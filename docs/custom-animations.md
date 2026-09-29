# Custom animations

Porchlight's Lighting page can play your own LED animations on any RGB device it controls. An
animation is a small text file (JSON) that lists color "frames" - no code, so importing one can
never run anything on your PC. You don't need to write it yourself: an AI chat can write it for you
from a one-line description.

## Make one with AI (easiest)

1. On the **Lighting** page, in the **Custom animations** card, click **Copy AI prompt**.
2. Open any AI chat (ChatGPT, Claude, Gemini, Copilot, ...) and paste.
3. Replace the two parts in `[brackets]` - what you want, and which device it's for - and send it.
4. Copy the AI's whole answer (a code block starting with `{`), go back to Porchlight, and click
   **Paste from clipboard**. The Markdown code fence around the answer is fine - Porchlight strips
   it.
5. In the **Effects** card, pick **Custom animation** as a device's effect, then pick your
   animation. The **Speed** slider plays it slower or faster.

If Porchlight says it couldn't import the answer, the message says exactly what's wrong (for
example `Frame 3, row 2: 'X' is not in the palette.`). Paste that message back into the same AI
chat and ask it to fix the animation, then paste again. Importing an animation with the same name
as an existing one replaces it, and devices already playing it switch to the new version.

You can also save an animation as a `.json` file and use **Import animation file...** - handy for
sharing one with family. Imported animations are kept in `%APPDATA%\Porchlight\Animations`, so
the original file can be moved or deleted afterwards. **Remove** deletes one from Porchlight.

## Ideas to ask for

- "A slow ocean wave in blues and teals that fades between colors."
- "A green progress bar that fills my keyboard from left to right over 5 seconds, then resets."
- "The word HI scrolling across my 27x7 keyboard in white on a dark blue background."
- "A gentle warm-white candle flicker."
- "Christmas lights: alternating red and green that swap every half second."

Tip: tell the AI your keyboard's grid size (a Logitech G915 is 27 columns x 7 rows) for per-key
pixel art. For a motherboard or RAM strip, say how many LEDs it has, or just ask for something that
uses `fill`/`gradient`, which look right on every device.

## The AI prompt template

This is exactly what **Copy AI prompt** puts on the clipboard.

```text
You are creating a custom RGB lighting animation for Porchlight, a Windows PC helper app.
Reply with ONE JSON object in the "porchlight-animation" format below and nothing else.

My animation idea: [DESCRIBE YOUR ANIMATION HERE]
It will play on: [YOUR DEVICE, e.g. "Logitech G915 keyboard (27x7 grid)" or "motherboard strip with 5 LEDs"]

FORMAT
{
  "format": "porchlight-animation",
  "version": 1,
  "name": "Short name (max 60 characters)",
  "description": "One sentence (optional)",
  "loop": true,                  // true = repeat forever, false = stop on the last frame
  "transition": "cut",           // "cut" = hard switch between frames, "fade" = blend smoothly
  "frameDuration": 0.1,          // default seconds per frame (0.02 to 600)
  "palette": { ".": "#000000", "R": "#FF0000" },  // one character -> "#RRGGBB" color
  "frames": [ ... ]              // 1 to 1000 frames, played in order
}

Each frame has exactly ONE of:
- "fill": "#RRGGBB" (or a palette character) - every LED shows one color.
- "gradient": ["#FF0000", "#0000FF", ...] - 2 to 64 colors blended smoothly left to right.
- "rows": ["R..R", ".RR."] - pixel art: each string is one row (top to bottom), each character
  one pixel (left to right) looked up in "palette". All rows must be the same length; at most
  64 rows of 64 characters.
A frame may also have "duration": seconds, overriding "frameDuration" for that frame.

HOW IT MAPS ONTO THE LEDS
The frame's grid is stretched over the device: the first/last column lines up with the
leftmost/rightmost LED and the first/last row with the top/bottom. So:
- For a keyboard, make "rows" exactly the keyboard's grid size (e.g. 27 characters x 7 rows
  for a Logitech G915) so each character is one key.
- For an LED strip (one row of LEDs), use "fill", "gradient", or a single row whose length
  equals the number of LEDs. Only the middle row of a taller grid is shown on a strip.
- "fill" and "gradient" work on every device.

RULES
- Colors are always "#RRGGBB" (6 hex digits). "#000000" is off.
- Every character used in "rows" must be a key in "palette" (keys are single characters).
- Movement comes from many frames with small changes, e.g. a dot moving one column per frame.
  Aim for 0.03-0.2 seconds per frame for motion and "fade" for slow color changes.
- Keep it under 1000 frames and 500 KB. No comments in your final answer.

EXAMPLE (a red/blue police flash that works on any device)
{
  "format": "porchlight-animation",
  "version": 1,
  "name": "Police lights",
  "loop": true,
  "transition": "cut",
  "frameDuration": 0.15,
  "palette": { ".": "#000000", "R": "#FF0000", "B": "#0000FF" },
  "frames": [
    { "rows": ["RRRR........"] },
    { "rows": ["............"] },
    { "rows": ["RRRR........"] },
    { "rows": ["............"], "duration": 0.3 },
    { "rows": ["........BBBB"] },
    { "rows": ["............"] },
    { "rows": ["........BBBB"] },
    { "rows": ["............"], "duration": 0.3 }
  ]
}
```

## Examples

Ready-to-import examples are in [`docs/animations/`](animations/):

| File | What it does | Works on |
| --- | --- | --- |
| [`police-lights.json`](animations/police-lights.json) | Red then blue double flashes, left then right | Any device |
| [`sunset.json`](animations/sunset.json) | A slow `fade` through sunset gradients | Any device |
| [`heartbeat.json`](animations/heartbeat.json) | A red double-beat pulse using per-frame `duration` | Any device |
| [`keyboard-scanner.json`](animations/keyboard-scanner.json) | A red bar sweeping back and forth, one column per frame | 27x7 keyboard (stretched on others) |

A minimal animation that works on any device:

```json
{
  "format": "porchlight-animation",
  "version": 1,
  "name": "Traffic light",
  "loop": true,
  "transition": "cut",
  "frames": [
    { "fill": "#00FF00", "duration": 3 },
    { "fill": "#FFB000", "duration": 1 },
    { "fill": "#FF0000", "duration": 3 }
  ]
}
```

## Format reference

| Field | Required | Meaning |
| --- | --- | --- |
| `format` | no | If present, must be `"porchlight-animation"`. |
| `version` | no | If present, must be `1`. |
| `name` | **yes** | Shown in Porchlight, at most 60 characters. Also decides the stored file name, so two animations with the same name replace each other. |
| `description`, `author` | no | Shown under the name in the list (description) / kept for reference (author). |
| `loop` | no | `true` (default) repeats forever; `false` plays once and holds the last frame. |
| `transition` | no | `"cut"` (default) switches frames instantly; `"fade"` blends each frame into the next over its duration. |
| `frameDuration` | no | Default seconds per frame, `0.02` to `600` (default `0.1`). |
| `palette` | only for `rows` | Maps single characters to `"#RRGGBB"` colors, at most 64 entries. |
| `frames` | **yes** | 1 to 1000 frames, played in order. |

Each frame has exactly one of `fill` (one color), `gradient` (2-64 colors blended left to right) or
`rows` (1-64 strings of 1-64 palette characters, all the same length), plus an optional `duration`
in seconds. Anywhere a color is expected in `fill`/`gradient`, a palette character works too.

**How a frame maps onto a device.** The frame's grid is stretched over the device's LED layout so
its first and last columns line up with the leftmost and rightmost LEDs, and its first and last
rows with the top and bottom. A grid the same size as a keyboard's matrix therefore maps one
character to one key; a single-row grid maps along an LED strip; a strip (one row of LEDs) shows the
middle row of a taller grid; a single-LED device shows the center pixel.

**Limits.** At most 512 KB per file, 1000 frames, and a 64x64 grid. Comments (`//`) and trailing
commas are tolerated because AI answers often contain them.
