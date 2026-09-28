# 05 - RGB lighting (branch `feat/lighting`)

> **Amended after 01b:** OpenRGB is the `openrgb` component from milestone 01b. The setup card below becomes the shared `ComponentCard` (Install -> Start with `--server --startminimized` -> Connect). Honour the "Start OpenRGB with Porchlight" setting from 01b: 01b already reads `Settings.Lighting.AutoStartOpenRgb` at app startup and starts OpenRGB if needed (`OpenRgbAutoStartHostedService`), but has no UI for the toggle itself - add a "Start OpenRGB with Porchlight" checkbox on this page's settings/setup area, bound (via `ISettingsStore.Update`) to `Settings.Lighting.AutoStartOpenRgb`, the same field.

Goal: control RGB on all supported devices from one page by talking to OpenRGB, which already supports hundreds of motherboards, RAM sticks, GPUs, keyboards, mice and fans. Porchlight does not talk to RGB hardware directly.

## Integration

- Library: `OpenRGB.NET` (3.1.x). Connects to the OpenRGB SDK server (default `127.0.0.1:6742`).
- If OpenRGB is not running or the SDK server is off, the page shows a setup card: (1) install OpenRGB (show the exact winget command if a winget package exists - verify the id with `winget search OpenRGB`; otherwise link openrgb.org), (2) start it with the SDK server enabled (`--server`, or SDK Server tab -> Start Server), (3) "Retry connection" button. Optional: if an OpenRGB executable path is set in settings, a "Start OpenRGB" button launches it with `--server --startminimized`.
- Host and port are configurable in the `Lighting` settings section.

## Core (`Porchlight.Core/Lighting`)

- `ILightingService`: `ConnectAsync(ct)`, `IsConnected`, `GetDevicesAsync(ct)` -> `RgbDevice(Index, Name, Type, Vendor, Modes[], ActiveMode, LedCount, Zones[])`, `SetDeviceColorAsync(index, RgbColor, ct)`, `SetAllColorAsync(RgbColor, ct)`, `SetModeAsync(index, modeName, ct)`, `GetProfilesAsync(ct)`, `LoadProfileAsync(name, ct)`, `TurnOffAllAsync(ct)` (all black), `Disconnected` event.
- All calls are off the UI thread, time out after 3 s, and serialize access to the client (the OpenRGB client is not thread-safe - use a `SemaphoreSlim`). A dropped connection raises `Disconnected` and the page returns to a "Reconnect" state; no exception reaches the UI.
- Setting a color on a device switches it to its "Direct" or "Static" mode first when available (prefer Direct), mirroring what OpenRGB's own UI does.
- `RgbColor` record with hex parse/format (`#RRGGBB`) and brightness scaling (`Scale(double 0..1)`), unit tested.

## App (`Porchlight.App/Features/Lighting`)

- Header with connection state (icon + text: "Connected to OpenRGB - 7 devices" / "Not connected").
- "All devices" card: color picker (hue/saturation square + hue slider, or a simple preset swatch grid + hex box - keep it simple and keyboard accessible), brightness slider, Apply to all, Turn off all, up to 8 saved favorite colors (persisted).
- Device list: one row per device with type icon, name, current mode (combo box of its modes), a color swatch button to set just that device.
- Profiles card: list OpenRGB profiles with "Load".
- Live preview is not required; apply on button press (and on swatch click).

## Tests

`RgbColor` parse/format/scale; mode-selection rule (prefers Direct, then Static, else leaves mode); service behaviour on timeout/disconnect using a fake client interface wrapping OpenRGB.NET.

## Acceptance criteria

- [ ] Without OpenRGB running: setup card shows, no exceptions, Retry works once OpenRGB is started.
- [ ] With OpenRGB running: devices listed; Apply to all changes every device; per-device color and mode work; Turn off all works; profiles load.
- [ ] Closing OpenRGB while connected shows the reconnect state without a crash.

## Addendum (branch `feat/lighting-color-wheel`): color wheel picker, conflict warning, exclusion, auto-start reliability

### Color wheel picker

The plain hex box described above is replaced by a reusable `ColorWheelPicker` control
(`Porchlight.App/Controls/ColorWheelPicker.xaml(.cs)`), used both by the "All devices" card and by
a popup next to each device row (per-device color):

- An HSV hue/saturation disc (drag or click) plus a value (brightness-of-the-hue) slider, a live
  preview swatch, and a hex/RGB box - all kept in sync both ways with the control's
  `SelectedColorHex` dependency property (two-way bindable like any other control).
- The wheel bitmap is generated once per pixel size into a cached `WriteableBitmap`
  (`ColorWheelPicker.GenerateWheelBitmap`, cached statically by size) - never per-pixel UI elements,
  and never regenerated on every drag move. Only the thumb position, preview swatch and value
  slider update per interaction.
- Mouse drag and click both work; arrow keys (Left/Right = hue, Up/Down = saturation) move the
  selection with the wheel focused, which shows a visible focus ring (`IsKeyboardFocusWithin`
  trigger). Every interactive part has an `AutomationProperties.Name`.
- The page's existing "Brightness" slider (which scales the final color sent to a device via
  `RgbColor.Scale`) is unchanged and separate from the wheel's own Value slider (which is the HSV
  value of the *picked color itself* - e.g. dark red vs. bright red); the two compose rather than
  overlap.
- Favorite colors (up to 8, persisted in `LightingSettings.FavoriteColors`) are unchanged - still
  saved/removed via the existing buttons/swatches next to the wheel.
- Dragging the wheel or value slider pushes the color live (so the device visibly follows the
  drag), throttled to at most ~20 calls/second via `Porchlight.Core.Lighting.ColorApplyRateLimiter`
  (a pure decision class, unit tested with a fake clock - no timer/I/O of its own). The final value
  on mouse-up/key-up/hex-commit is always sent, bypassing the limiter. `ColorWheelPicker` itself
  raises `ColorPicked(RgbColor, bool isFinal)`; `LightingViewModel.OnAllDevicesColorPicked` and
  `DeviceRowViewModel.OnColorPicked` apply the throttling decision and actually call
  `ILightingService`.
- Color math (HSV<->RGB conversion, wheel-point<->hue/saturation mapping) lives in
  `Porchlight.Core.Lighting.HsvColor` and `ColorWheelMath` - plain, WPF-free, unit tested classes.

### Lighting conflict warning

Other software can also claim control of the same RGB device OpenRGB is driving - most visibly, a
device's lighting periodically turning itself off as two controllers fight over it.
`Porchlight.Core.Lighting.ILightingConflictDetector`/`LightingConflictDetector` detects, read-only
(never changes a setting, stops a process, or touches OpenRGB itself):

- **Windows Dynamic Lighting**: `HKCU\Software\Microsoft\Lighting`, `AmbientLightingEnabled` = 1
  means it's on. If `Brightness` = 0, a specific message is shown ("...brightness is 0% - devices
  it controls will turn off...") since that alone turns its controlled devices off even though the
  feature itself is "on". Read via a new `IRegistryReader.GetCurrentUserDwordValue(subKey,
  valueName)` member (reusing the existing registry-detection interface/pattern from
  `ComponentService`, rather than a new one).
- **Vendor RGB software** (`VendorLightingSoftware.All`): Logitech G HUB, Razer Synapse, Corsair
  iCUE, SteelSeries GG, ASUS Armoury Crate/Aura, MSI Mystic Light/Center, Gigabyte RGB Fusion,
  SignalRGB, NZXT CAM, HyperX NGenuity - detected via `IProcessProbe.IsRunning` (its process) or
  `IRegistryReader.ServiceExists` (its background service, e.g. `logi_lamparray_service`,
  `ArmouryCrate.Service`, `AuraWallpaperService` - present even when the vendor's tray app isn't
  currently open). Process/service names are the well-known ones for each vendor's current
  shipping software - a best-effort catalog, not a guarantee of detecting every version.

The Lighting page shows a dismissible (per app session only - not persisted) warning panel listing
every detected conflict with plain-words advice; Windows Dynamic Lighting's entry has an "Open
Dynamic Lighting settings" button (`ms-settings:personalization-lighting`, opened via the existing
`IUrlLauncher`, shared with the RemoteSupport feature). Shown regardless of `ShowSetup`/
`IsConnected` - it's about other software, not about OpenRGB's own connection.

### Per-device exclusion

A device row has a "Don't control this device" checkbox (`DeviceRowViewModel.IsExcluded`,
persisted as device names in `LightingSettings.ExcludedDeviceNames`) - e.g. to leave a keyboard to
its vendor's own software (G HUB) instead of Porchlight/OpenRGB. Excluded from "Apply to
all"/"Turn off all"; still settable individually (its swatch button and color-wheel popup are
unaffected). When nothing is excluded, "Apply to all"/"Turn off all" still use
`ILightingService`'s bulk `SetAllColorAsync`/`TurnOffAllAsync` unchanged (OpenRGB's own bulk API
has no per-device opt-out, so an exclusion falls back to setting devices one at a time, skipping
the excluded one, only once at least one device actually is excluded).

### Auto-reconnect

`LightingViewModel` now retries the connection every 10 seconds while disconnected
(`FireAndForgetReconnectAttempt`, started on `ILightingService.Disconnected` or a failed
connect/refresh, stopped as soon as a connection succeeds) instead of requiring the user to click
Retry once OpenRGB becomes reachable again.

### OpenRGB auto-start reliability

Bug (maintainer's PC): with `Settings.Lighting.AutoStartOpenRgb` on, OpenRGB
(`ComponentCatalog`'s `--server --startminimized`) reliably exited 5-10 seconds after Porchlight
started it, while running the identical command manually from a shell stayed up indefinitely - so
something about *how* Porchlight launched it, not the command itself, ended it early.
`ProcessRunner.StartDetached` previously used `UseShellExecute = false` with `using var process =
Process.Start(startInfo);`. It now launches through the shell (`UseShellExecute = true`,
`ProcessRunner.BuildDetachedStartInfo`), with no stdio redirection at all, and the returned
`Process` is never captured in a `using`/disposed/killed by Porchlight - launching through the
shell means Porchlight is never the child's direct process creator/owner, so it can't be torn down
by anything tied to Porchlight's own process (a job object, a closed pipe) - the standard fix for a
"must outlive the launcher" detached process start on Windows. `RunAsync` (winget, etc. - processes
whose output Porchlight actually reads and that are meant to be tied to Porchlight's own
cancellation) is unaffected; this only changes `StartDetached`, used solely for OpenRGB's
fire-and-forget launch.

### Tests

`HsvColorTests`, `ColorWheelMathTests` (RGB<->HSV and wheel-point<->hue/saturation round trips and
known values), `ColorApplyRateLimiterTests` (throttling behavior with a fake clock),
`LightingConflictDetectorTests` (Windows Dynamic Lighting on/off/zero-brightness, each vendor,
multiple conflicts, read-only), `LightingViewModelTests`/`DeviceRowViewModelTests` (conflict
panel/dismiss/action button, exclusion persistence, live-color throttling),
`ProcessRunnerTests.BuildDetachedStartInfo_UsesShellExecuteAndNoRedirection` (the auto-start fix).

### Acceptance criteria (addendum)

- [ ] The color wheel picker sets hue/saturation by drag, click, and arrow keys; the hex box stays
      in sync both ways; favorites still save/apply.
- [ ] Dragging the wheel visibly updates a connected device's color without flooding OpenRGB (no
      lag/disconnect from a fast drag); the final dragged-to color is always applied.
- [ ] With Windows Dynamic Lighting on, the Lighting page shows a warning with a working "Open
      Dynamic Lighting settings" button; dismissing it hides it for the rest of the session.
- [ ] With a vendor RGB app (e.g. Logitech G HUB) running, the Lighting page shows a warning naming
      it.
- [ ] A device marked "Don't control this device" is skipped by "Apply to all"/"Turn off all" but
      still individually controllable.
- [ ] With `AutoStartOpenRgb` on, OpenRGB started by Porchlight stays running (does not exit after
      a few seconds).
- [ ] After OpenRGB becomes reachable again post-disconnect, the page reconnects on its own within
      ~10 seconds without the user clicking Retry.
