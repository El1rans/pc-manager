# 05 - RGB lighting (branch `feat/lighting`)

> **Amended after 01b:** OpenRGB is the `openrgb` component from milestone 01b. The setup card below becomes the shared `ComponentCard` (Install -> Start with `--server --startminimized` -> Connect). Honour the "Start OpenRGB with PC Manager" setting from 01b: 01b already reads `Settings.Lighting.AutoStartOpenRgb` at app startup and starts OpenRGB if needed (`OpenRgbAutoStartHostedService`), but has no UI for the toggle itself - add a "Start OpenRGB with PC Manager" checkbox on this page's settings/setup area, bound (via `ISettingsStore.Update`) to `Settings.Lighting.AutoStartOpenRgb`, the same field.

Goal: control RGB on all supported devices from one page by talking to OpenRGB, which already supports hundreds of motherboards, RAM sticks, GPUs, keyboards, mice and fans. PC Manager does not talk to RGB hardware directly.

## Integration

- Library: `OpenRGB.NET` (3.1.x). Connects to the OpenRGB SDK server (default `127.0.0.1:6742`).
- If OpenRGB is not running or the SDK server is off, the page shows a setup card: (1) install OpenRGB (show the exact winget command if a winget package exists - verify the id with `winget search OpenRGB`; otherwise link openrgb.org), (2) start it with the SDK server enabled (`--server`, or SDK Server tab -> Start Server), (3) "Retry connection" button. Optional: if an OpenRGB executable path is set in settings, a "Start OpenRGB" button launches it with `--server --startminimized`.
- Host and port are configurable in the `Lighting` settings section.

## Core (`PCManager.Core/Lighting`)

- `ILightingService`: `ConnectAsync(ct)`, `IsConnected`, `GetDevicesAsync(ct)` -> `RgbDevice(Index, Name, Type, Vendor, Modes[], ActiveMode, LedCount, Zones[])`, `SetDeviceColorAsync(index, RgbColor, ct)`, `SetAllColorAsync(RgbColor, ct)`, `SetModeAsync(index, modeName, ct)`, `GetProfilesAsync(ct)`, `LoadProfileAsync(name, ct)`, `TurnOffAllAsync(ct)` (all black), `Disconnected` event.
- All calls are off the UI thread, time out after 3 s, and serialize access to the client (the OpenRGB client is not thread-safe - use a `SemaphoreSlim`). A dropped connection raises `Disconnected` and the page returns to a "Reconnect" state; no exception reaches the UI.
- Setting a color on a device switches it to its "Direct" or "Static" mode first when available (prefer Direct), mirroring what OpenRGB's own UI does.
- `RgbColor` record with hex parse/format (`#RRGGBB`) and brightness scaling (`Scale(double 0..1)`), unit tested.

## App (`PCManager.App/Features/Lighting`)

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
