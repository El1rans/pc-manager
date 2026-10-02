# Get apps

**Where to find it:** Apps & services > Get apps.

Installing a program usually means a web search and a download page full of look-alike "Download"
buttons - an easy way to end up with the wrong thing. Get apps installs programs straight from the
official Windows package catalog (`winget`), so there is nothing to download by hand.

![Get apps page with a search box and a list of popular apps, each with an Install button](../screenshots/get-apps.png)

*(Screenshot uses made-up demo data, not a real PC.)*

## What's on the page

- **Search box and Search button** - type at least two letters of the program's name and press
  **Enter** or **Search**. Searching takes a few seconds; a progress bar shows while it runs.
- **Popular apps** - shown while the search box is empty: a short list of well-known programs
  (Chrome, Firefox, VLC, 7-Zip, Zoom, Spotify, Adobe Acrobat Reader, Notepad++, Discord).
- **Results** - up to 50 matches, each with its name, version and catalog ID.
- **Message line** - how many apps were found, "No apps found..." with a tip, or a plain note if
  `winget` isn't available on this PC.

## Each app in the list

- **Install** - installs the app. Only one app installs at a time; the other Install buttons wait
  until it finishes. A line under the app shows what the installer is doing.
- **Installed** - shown instead of the button when the app is already on the PC.
- **Result** - "Installed", "Installed. Restart your PC to finish.", or a plain sentence saying
  what went wrong and what to try next.

Some installers show their own window or ask for administrator approval (a Windows prompt). If
**Silent install** is ticked on the Updates page, installers run without their own window where
they can. An install that has started is never stopped half way; if you try to close Porchlight
during one, it warns you first.

Every finished install also appears in the Updates page's [history](updates.md#update-history).

Only the community `winget` catalog is searched - not the Microsoft Store.

---

[Back to the page list](../../README.md#pages)
