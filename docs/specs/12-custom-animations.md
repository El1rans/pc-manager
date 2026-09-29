# 12 - Custom animations (branch `claude/custom-animations-porchlight-d698uo`)

The LED effects engine (docs/specs/11-led-effects.md) ships a fixed set of built-in effects. This
milestone lets the user add their own: upload an animation file (or paste one an AI chat wrote) on
the Lighting page and assign it to any device like a built-in effect. The user-facing guide, with
the AI prompt template and examples, is docs/custom-animations.md.

## Goals

- A declarative, data-only animation format (`porchlight-animation` JSON, version 1) that is easy
  for a person or an AI chat to write: color frames made of a solid `fill`, a left-to-right
  `gradient`, or `rows` of palette-character pixel art, with optional per-frame durations, looping
  and cut/fade transitions.
- Works on every layout the engine already supports - a keyboard matrix, a linear strip, a single
  LED - by stretching the frame's grid over the device's normalized LED positions.
- Import from a file or straight from the clipboard, with a precise, plain-language error for
  anything wrong, short enough to paste back into the AI chat to get it fixed.
- A "Copy AI prompt" button with a ready-made prompt template that teaches any AI chat the format.

## Non-goals

- **No scripting.** Porchlight runs elevated; an animation is data only and can never execute
  anything. Anything that needs code (reacting to CPU load, audio, ...) stays a built-in effect.
- Sharing/downloading animations from an online gallery; an in-app animation editor; per-zone
  assignment (same limitation as spec 11).

## Design

### Core (`Porchlight.Core.Lighting.Effects.CustomAnimations`)

- **`CustomAnimationParser.Parse(json)`** - `System.Text.Json` `JsonDocument` with comments and
  trailing commas allowed (AI answers often contain them) and a leading/trailing Markdown code
  fence stripped. Never throws for bad input: returns `CustomAnimationParseResult` with either the
  animation or an error naming the frame/row/character at fault. Every limit is checked before
  allocating for it: 512 KB document, 1000 frames, 64x64 grid, 64 palette entries, 60-character
  name, frame durations 0.02-600 s. Unknown properties are ignored (forward compatible).
- **`CustomAnimation` / `CustomAnimationFrame`** - immutable. `PositionAt(elapsed)` returns the
  current frame, the next one and the fade blend (0 for `cut`, and for the held last frame of a
  non-looping animation) via a binary search over precomputed frame start times.
  `CustomAnimationFrame.Sample(x, y)` maps normalized LED coordinates onto the grid so its first/last
  column/row line up with the layout's edges - a grid the size of a keyboard matrix maps one
  character per key, a strip samples the middle row, a gradient blends between adjacent stops.
- **`CustomAnimationEffect : IEffect`** - pure and deterministic like every other effect; the
  UI's speed slider scales playback time.
- **`CustomAnimationLibrary : ICustomAnimationLibrary`** (DI singleton) - stores each imported
  animation as `%APPDATA%\Porchlight\Animations\<id>.json`, where `id` is a slug of its name
  ("Police Lights!" -> `police-lights`; a name with no ASCII letters/digits gets
  `animation-<hash>`). Re-importing the same name replaces the animation. Ids are re-validated
  (`[a-z0-9]+(-[a-z0-9]+)*`, max 40) before every disk access because they also come back from the
  user-editable settings file - no path can escape the folder. Files that no longer parse are
  skipped when listing. Files are written via a temp file + move.
- **`EffectRegistry`** gains `"Custom animation"` (`CustomAnimationEffectName`), built from the
  assignment's `animationId` and `speed` settings through an optional `ICustomAnimationLibrary`
  argument; it returns null (engine skips the device) when the library is absent or the animation
  was removed. `EffectEngine` takes the library as a new optional constructor parameter.
- **`CustomAnimationPrompt.Template`** - the AI prompt, with `[DESCRIBE YOUR ANIMATION HERE]` and
  `[YOUR DEVICE ...]` placeholders, the format, mapping rules and a worked example.

### App (`Porchlight.App/Features/Lighting`)

- The Effects card's per-device picker gains "Custom animation" (offered on every device). Picking
  it reveals an animation picker (defaulting to the first imported animation) and the existing
  speed slider, or a hint to import one first.
- A new "Custom animations" card: a short how-to, **Copy AI prompt** (`IClipboardService.SetText`),
  **Paste from clipboard** (new `IClipboardService.GetText`), **Import animation file...**
  (`IAnimationFilePicker`, a WPF `OpenFileDialog`), a status line with the import result/error,
  and the list of imported animations with **Remove**.
- Removing an animation clears it from any device playing it and re-syncs the engine; re-importing
  one a device is playing restarts the engine so the new version shows immediately.
  `LightingViewModel.CustomAnimations` is reconciled in place (not cleared and refilled) so each
  device's picker keeps its selection, and `DeviceRowViewModel.SelectedCustomAnimationId` ignores
  the `null` WPF's `ComboBox` writes back while its selected item is being replaced.

## Tests

Core: `CustomAnimationParserTests` (defaults, every field, comments/fences, every validation
error, size limits, control-character stripping), `CustomAnimationEffectTests` (cut/fade timing,
looping vs holding the last frame, speed, gradient spread, one-character-per-key matrix mapping
with a hole, middle-row sampling on a strip, determinism), `CustomAnimationLibraryTests`
(import/list/load/replace/remove, file size limit, id validation against path traversal, slugs
including non-Latin names, skipping broken files, registry and engine integration),
`CustomAnimationExamplesTests` (the prompt's example and format sketch, every `docs/animations`
file and every JSON block in docs/custom-animations.md parse; the guide shows the exact prompt).
App: `LightingViewModelTests` (copy prompt, paste valid/invalid/empty, file import and cancel,
default selection and persistence, remove clears selection, re-import keeps selection) and
`DeviceRowViewModelTests` (picker offers the effect, settings round-trip, null from the picker is
ignored, forgetting only the matching id).

## Acceptance criteria

- [ ] "Copy AI prompt" puts the prompt template on the clipboard; pasting an AI's answer (with or
      without a Markdown code fence) imports it via "Paste from clipboard".
- [ ] "Import animation file..." imports a `.json` animation; an invalid one shows a specific,
      plain-language error and imports nothing.
- [ ] "Custom animation" can be assigned to any device, plays the chosen animation at the chosen
      speed, and persists across an app restart.
- [ ] Removing an animation stops it on every device using it; re-importing one with the same name
      replaces it and devices playing it pick up the new version.
- [ ] An animation file can never cause code to run, a file outside the animations folder to be
      read or written, or unbounded memory use.
- [ ] Every new control is keyboard accessible and has an `AutomationProperties.Name`.
- [ ] `dotnet build -c Release` is warning-free; `dotnet test -c Release` passes.
