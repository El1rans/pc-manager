# 11 - Custom LED effects (branch `feat/led-effects-engine`)

Porchlight already drives RGB devices' static color/mode from the Lighting page
(docs/specs/05-lighting.md). This milestone adds a small effects engine that animates a device's
LEDs over time - a rainbow wave, a breathing color, a CPU-temperature gradient, a Pac-Man chase on
a keyboard's matrix, falling rain, and an "updates pending" overlay - and a Lighting page section to
assign one to each device.

## Goals

- A pure, unit-testable `IEffect` abstraction that renders one frame from elapsed time plus a small
  amount of context (CPU temperature, pending update count, recent key presses) onto a device's LED
  layout, with no knowledge of OpenRGB, WPF, or the dispatcher.
- A frame-loop engine (`EffectEngine`) that drives every assigned, non-excluded device at a
  configurable frame rate, switches it to Direct mode once, sends colors only when they change,
  isolates one device's failure from the rest, and always restores the device's previous mode when
  it stops.
- A Lighting page section to pick an effect (and its settings) per device, pause every effect at
  once, and toggle a global "updates pending" overlay.
- Real hardware in mind: a Logitech G915 keyboard (`MATRIX` zone, 27x7, 117 LEDs, Direct mode) and
  an ASUS B550-F motherboard (`Linear` zone, 5 LEDs) - the matrix-only effects must render sensibly
  on the former and simply do nothing (not throw) on the latter.

## Non-goals (this milestone)

- **No global keyboard hook.** `TypingRippleEffect` exists and is unit tested, but its
  `IKeyPressSource` has no real implementation yet - `NullKeyPressSource` (registered by default)
  never raises a key press, so the effect simply never animates. See "Phase 2" below. Because of
  this, "Typing ripple" is deliberately left off the Lighting page's effect picker for now (it would
  just look identical to "off").
- Per-zone effect assignment (a device with two zones only ever renders onto one - see
  `EffectEngine.SelectPrimaryZone`); per-LED custom patterns; importing/exporting effect profiles;
  syncing effects across multiple PCs.
- Audio-reactive effects.

## Design

### Core (`Porchlight.Core.Lighting.Effects`)

- **Layout**: `LedLayoutBuilder.Build(EffectZoneInfo, deviceLeds)` turns one OpenRGB zone into a
  `LedLayout` - a flat, normalized-`(0..1, 0..1)` list of `LedPoint`s an effect renders against,
  independent of OpenRGB:
  - `Matrix` zones use OpenRGB's own row/column matrix map, normalizing by width/height; a hole
    (OpenRGB's `0xFFFFFFFF`, translated to `-1`) produces no point.
  - `Linear` zones place LEDs in a single row, evenly spaced left to right.
  - `SingleLed` zones place the one LED at the center.
  - A zero-LED zone returns `LedLayout.Empty`. Every LED is named (`EffectLedInfo.Name`, e.g.
    "Key: A") by matching `EffectLedInfo.Index`, not by list position - a device's LED list is not
    necessarily aligned with a zone's own local indices.
- **Effect contract**: `IEffect.Render(in EffectFrame frame, Span<RgbColor> buffer)` is a pure
  function of `frame.Elapsed`, `frame.Layout`, and `frame.Context` (`IEffectContext`: nullable CPU
  temperature in °C, pending update count, recent key presses) - no timers, no I/O, deterministic
  for a given input, which is what makes every effect unit-testable without a real clock or device.
- **Effects (v1)**: `RainbowWaveEffect` (hue scrolls across normalized X), `BreathingEffect` (one
  color's brightness follows a raised-cosine wave - 0 at the start of each period, 1 at the half
  period), `CpuTemperatureEffect` (blue -> green -> amber -> red between configurable min/max °C,
  smoothed so a noisy sensor reading doesn't flicker the color), `UpdatesAlertEffect` (a composable
  overlay - wraps another effect and pulses a warning color over it while updates are pending, see
  `EffectRegistry.CreateWithOverlay`), `PacManEffect` (matrix-only: boustrophedon path across rows,
  eaten cells dark, a mouth-open/closed animation, a ghost trailing a few cells behind, refills once
  the path completes), `RainEffect` (matrix-only: one falling drop per column with a fading trail,
  each column phase-shifted so drops don't fall in lockstep). `TypingRippleEffect` exists
  (matrix-only: expanding rings from `IEffectContext.RecentKeyPresses`) but is not yet reachable from
  the UI - see "Non-goals" above.
- **`EffectRegistry`**: builds a named effect from an `EffectAssignment`'s string-keyed settings
  dictionary, falling back to that effect's own constructor defaults for a missing or unparsable
  value - a corrupt or stale settings file degrades gracefully instead of breaking the engine.
- **`EffectEngine`** (DI singleton, not started automatically): `Start(fps)` connects its own
  `IEffectDeviceClient` (a second, independent OpenRGB SDK connection from the Lighting page's own
  `ILightingService` - matched by device *name*, which is stable across reconnects, rather than
  OpenRGB's index, which is only stable for one connection's lifetime), switches every assigned,
  non-excluded device to Direct mode once, and starts a `TimeProvider`-driven periodic timer
  (default 30 fps, clamped 10-60). Each tick renders every active device's effect and calls
  `UpdateLeds` only if the frame actually changed since the last send. A device whose effect throws
  is logged and "paused" (skipped) for the rest of the run without affecting any other device.
  `Stop()` (also called from `Dispose()` and on app exit, since the engine is a disposed DI
  singleton) restores every device to whichever mode was active before `Start()`. Excluded devices
  (`IDeviceExclusionProvider` - `SettingsDeviceExclusionProvider`, backed by the same
  `LightingSettings.ExcludedDeviceNames` list the Lighting page's own "Apply to all"/"Turn off all"
  honour, see docs/specs/05-lighting.md's "Per-device exclusion" addendum) are never started.
- **Persistence**: `LightingSettings.EffectAssignments` (additive to the existing settings file) is
  a list of `EffectAssignment { DeviceKey, EffectName, ShowUpdatesAlert, Settings }`, keyed by device
  name for the same reason `EffectEngine` matches by name.

### App (`Porchlight.App/Features/Lighting`)

The existing Lighting page (docs/specs/05-lighting.md) gains an "Effects" card, below the device
list:

- A one-line note: "Custom effects run while Porchlight is open."
- A global "Updates alert overlay" checkbox, composed onto every device's assigned effect.
- A global "Pause effects"/"Resume effects" button that stops the engine (restoring every device's
  mode) without discarding the assignments, so resuming picks the same effects back up.
- One row per device (hidden for a device marked "Don't control this device" - see
  docs/specs/05-lighting.md's per-device exclusion): a picker - "None (device mode)", "Rainbow
  wave", "Breathing", "CPU temperature", "Pac-Man", "Rain" - with the last two offered only for a
  device that has a matrix zone (`DeviceRowViewModel.HasMatrixZone`, from `RgbZone.IsMatrix`).
  Selecting an effect reveals its settings: a 0.25x-3x speed slider (Rainbow wave, Breathing,
  Pac-Man, Rain), the shared `ColorWheelPicker` (Breathing), or a min/max °C pair (CPU temperature).
  Selecting "None" stops that device's effect and restores its device mode.
- Every change (effect, speed, color, temperature range, the global overlay toggle) persists to
  `LightingSettings.EffectAssignments` via `ISettingsStore.Update` and restarts the engine
  (`EffectEngine.SetAssignments` only applies on the next `Start`) so it takes effect immediately.
- Full keyboard access (`Tab`/arrow keys through the combo boxes, sliders, and the color wheel, which
  already supports arrow-key hue/saturation - see docs/specs/05-lighting.md's color wheel addendum)
  and an `AutomationProperties.Name` on every interactive control.

## Deviations from a literal reading of the brief

- `IPendingUpdateCountProvider` is a small seam the effects engine defines for itself, defaulting to
  `ZeroPendingUpdateCountProvider`: the Updates feature (`Porchlight.Core.Winget`) does not currently
  expose a pending-update count as a DI-injectable service (it's computed only inside the Updates
  page's own view model), and reaching into a UI-layer type from `Porchlight.Core` would invert the
  solution's layering. A later change can register a real implementation without `EffectEngine`
  changing at all.
- The "Updates alert overlay" toggle is global (applies to every device's assignment together)
  rather than per device, even though `EffectAssignment.ShowUpdatesAlert` is itself a per-device
  field - simpler for a first version; per-device control is a straightforward follow-up if wanted.
- The per-device speed slider is a single generic 0.25x-3x multiplier applied to whichever
  effect-specific rate setting the selected effect actually uses (`speed` for Rainbow
  wave/Breathing, `cellsPerSecond` for Pac-Man, `rowsPerSecond` for Rain) rather than a differently
  labeled/ranged slider per effect - keeps the UI to one control per row.

## Phase 2 (not in this PR): opt-in keyboard-reactive effects

`IKeyPressSource`/`KeyPressEvent`/`TypingRippleEffect` already exist so a later PR can add a real,
global low-level keyboard hook implementation and wire it into the Lighting page's effect picker.
That implementation must, from the start:

- Be **off by default**, behind its own explicit settings toggle the user turns on.
- **Never log, persist, or otherwise store which key was pressed** - not the character, not the scan
  code, not the virtual key code. `IKeyPressSource.KeyPressed` already only carries a
  `KeyPressEvent` translated to an LED grid position (`Col`/`Row`) plus a timestamp; the real hook
  must translate at the point of capture and discard the original key event immediately rather than
  passing it further up the stack.
- Be clearly disclosed in the settings UI next to its toggle (what it does, that it is local-only,
  that Porchlight does not log keystrokes) before the user turns it on.
- Stop hooking immediately when the toggle is turned off or the app exits.

## Tests

`LedLayoutBuilderTests` (matrix/linear/single-LED layout, hole-skipping, LED naming, the real G915
27x7/117-LED and B550-F 5-LED linear cases); `RainbowWaveEffectTests`, `BreathingEffectTests`,
`CpuTemperatureEffectTests`, `PacManEffectTests`, `RainEffectTests` (each effect's determinism for a
fixed elapsed time, its key visual property - e.g. Pac-Man's eaten-cell/ghost-lag rule, Rain's
per-column phase spread, Breathing's period/peak - and matrix-only effects rendering nothing on a
non-matrix layout); `EffectRegistryTests` (every name builds the right type, unknown name/unparsable
setting falls back safely, the updates-alert overlay composes); `EffectModeSelectorTests`;
`SettingsDeviceExclusionProviderTests`; `EffectEngineTests` (fps cap, change-only sends, per-device
error isolation, mode restore on `Stop`, exclusion respected - all using a `FakeTimeProvider` stepped
one frame period at a time, since `FakeTimeProvider.Advance` sets its clock to the final time before
running any due periodic callbacks, so a single large advance would have every tick observe the same
elapsed time); `DeviceRowViewModelTests`/`LightingViewModelTests` (matrix-only effects hidden for a
non-matrix device, effect selection persisting to settings and restarting the engine, "None"
clearing the assignment, the global pause button).

## Acceptance criteria

- [ ] `EffectEngine.Start` connects, switches every assigned, non-excluded device to Direct mode,
      and starts rendering; `Stop`/`Dispose`/app exit restores each device's previous mode.
- [ ] Frame rate never exceeds the configured fps; an unchanged frame is not re-sent; one device's
      effect throwing does not stop any other device.
- [ ] A device marked "Don't control this device" is never started by the engine, even if it has an
      effect assigned.
- [ ] Matrix-only effects (Pac-Man, Rain) are only offered in the picker for a device with a matrix
      zone, and render nothing (not an exception) if ever run against a non-matrix layout.
- [ ] Selecting "None" stops that device's effect and restores its mode; every other effect choice
      and setting persists across an app restart.
- [ ] The global "Pause effects" button stops every running effect and restores every device's mode
      without discarding the per-device assignments; "Resume" picks them back up.
- [ ] The effects section is keyboard accessible and every control has an `AutomationProperties.Name`.
- [ ] `dotnet build -c Release` is warning-free; `dotnet test -c Release` passes.
