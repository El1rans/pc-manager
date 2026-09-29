namespace Porchlight.Core.Lighting.Effects.CustomAnimations;

/// <summary>
/// The ready-to-paste prompt behind the Lighting page's "Copy AI prompt" button: it teaches any AI
/// chat the <c>porchlight-animation</c> format (see docs/custom-animations.md, which carries the
/// same text) so the user only has to describe the animation they want and paste the answer back
/// with "Paste from clipboard". Kept in sync with <see cref="CustomAnimationParser"/>'s rules and
/// limits - <c>CustomAnimationPromptTests</c> parses the example it contains.
/// </summary>
public static class CustomAnimationPrompt
{
    /// <summary>Where the user types their idea, replaced in <see cref="Template"/>.</summary>
    public const string IdeaPlaceholder = "[DESCRIBE YOUR ANIMATION HERE]";

    /// <summary>Where the user says which device(s) it's for, replaced in <see cref="Template"/>.</summary>
    public const string DevicePlaceholder = "[YOUR DEVICE, e.g. \"Logitech G915 keyboard (27x7 grid)\" or \"motherboard strip with 5 LEDs\"]";

    public const string Template = """
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
        """;
}
