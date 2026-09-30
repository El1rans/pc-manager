# Hardware

**Where to find it:** Hardware > Sensors & fans (the tab is called "Sensors & fans"; the page heading is "Hardware"). Lighting is the other tab in this category.

Shows what the PC's parts are doing - temperatures, speeds, power, fan speeds - and, if you want, lets Porchlight control the fans. Full sensor access and fan control need **administrator rights** and, for many PCs, the optional **PawnIO** driver (see [Set up optional features](set-up-optional-features.md)).

![Hardware page with tiles for CPU temperature, GPU temperature, CPU power and hottest fan, above collapsed cards for the processor, graphics card and other components](../screenshots/hardware.png)

*(Screenshot uses made-up demo data, not a real PC.)*

## Above the tabs

- **Administrator banner** - "Sign in as administrator to see every sensor and control fans", with a **Restart as administrator** button. Shown until Porchlight runs elevated.
- **Driver card (PawnIO)** - shown when the optional PawnIO driver is missing; lets you install it. It is what allows reading the extra sensors and controlling fans.
- **Critical temperature warning** (red) - appears if a part is dangerously hot.

## Sensors tab

- **Summary tiles** - up to four big "at a glance" numbers: **CPU temperature**, **GPU temperature**, **CPU package power**, and **Hottest fan** (each with a small detail line, and a warning line if it is running hot).
- **Filter** - type to narrow the list by hardware or sensor name.
- **Hide unused sensors** - hides sensors that report nothing useful.
- **Reset min/max** - clears the recorded lowest and highest readings.
- **Hardware cards** - one collapsible card per part (processor, graphics card, motherboard chip, memory, drives...). Collapsed, each shows a one-line summary; open it for a table of every sensor with **Current**, **Min** and **Max** values.

## Fans tab

- **Conflict warning** - shown if other fan software (for example ASUS Armoury Crate) is also controlling the fans, because its settings may override Porchlight's.
- **Global fan control** - **Software fan control** (off by default; when off, the BIOS is in charge), an **Overheat failsafe temperature** slider (fans go to full speed above it), a **Minimum fan percent** slider, and **Restore BIOS control for all fans**.
- **One card per fan** - name (with a **Rename** button; **Save** / **Cancel** while renaming), current speed in RPM and percent, a **mode** picker (default / fixed percent / curve), the **Fixed percent** value, the **Source temperature sensor** it follows, and, for a curve, the curve points with **Add point** and an error line if the curve is invalid.
- If Windows does not expose any fan controls on this PC, the tab says so.

---

[Back to the page list](../../README.md#pages)
