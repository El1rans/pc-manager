# Updates

**Where to find it:** Tune-up > Updates. The Tune-up item in the left menu also shows a small number badge (how many updates are waiting), and so does the Updates tab.

This page keeps the programs on the PC up to date. It lists every program that has a newer version available, lets you pick which ones to update, and installs them one after another. Porchlight only looks for updates by itself; it never installs anything until you press the button. It uses Windows' built-in `winget` tool behind the scenes.

![Updates page listing programs with an installed and an available version, checkboxes to pick which to update, and an Update selected button](../screenshots/updates.png)

*(Screenshot uses made-up demo data, not a real PC.)*

## Top of the page

- **Summary line** - how many updates are available and when the list was last checked.
- **Refresh** - checks again for updates now.
- **Select all / Select none** - tick or untick every program in the list at once.
- **Filter** - type part of a program's name to narrow the list.
- **Show ignored** - also shows programs you have told Porchlight to never update (they are normally hidden).

## The list

Each row is one program.

- **Tick box** - whether this program will be updated when you press **Update selected**.
- **Name** and **ID** - the program's name and its technical package name.
- **Installed** and **Available** - the version on the PC now, and the newer version that can be installed. "Unknown" means Windows cannot tell what is installed.
- **Notes** - warnings, for example that the current version is unknown (updating may install a second copy) or that a program is pinned and only updates when asked for specifically.
- **Status** - what is happening to that program while updating (waiting, updating, done, failed, ...). Hover for a fuller explanation.

Right-click a row for extras: **Ignore (never update)**, **Stop ignoring**, **Copy ID**, and **Show package info**.

When a row is selected, a details strip appears with its status explanation and any buttons that apply to it: **Reinstall...** (with a confirmation step), **Hide this update**, and a **retry** button after a failure.

## Options and the main button

- **Silent install (hide installer windows)** - installs without showing each program's installer window.
- **Include apps with unknown version** - also offers programs whose current version cannot be read.
- **Check for updates when Porchlight starts** - looks for updates each time Porchlight opens.
- **Update selected (N)** - installs every ticked program. While it runs, a **Stop after current** button lets you stop after the program in progress finishes, and a progress bar shows the current step.

## Move to a new PC (collapsed section)

Helps when you get a new computer. **Save my app list...** writes the list of installed programs to a file; on the new PC, **Install apps from a list...** reads that file, shows you what will be installed, and installs it once you click **Install these apps**.

## Log (collapsed section)

The detailed output from the update tool, for troubleshooting or to show a helper. **Open log folder** opens the folder where logs are kept; **Clear** empties the on-screen log.

Some updates need administrator rights; Porchlight tells you if one does.

---

[Back to the page list](../../README.md#pages)
