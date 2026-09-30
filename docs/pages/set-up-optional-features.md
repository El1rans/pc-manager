# Set up optional features

**Where to find it:** the **Set up optional features** button at the bottom of the left menu. It opens a small window, not a page. The same window can also appear when Porchlight first starts.

Porchlight works on its own, but three tools add extra abilities. This window installs them for you, using Windows' `winget` tool, so an internet connection is needed. Installing needs administrator rights, and if `winget` is missing Porchlight tells you so.

## What's in the window

- **Heading and introduction** - "Choose what to set up".
- **One card per optional tool**, each with its name, what it is for, and its current status (for example not installed, installed, or a progress/error message while installing):
  - **AnyDesk** - remote help from family; needed by the [Get help](get-help.md) page.
  - **OpenRGB** - RGB lighting control; needed by the [Lighting](lighting.md) page.
  - **PawnIO driver** - fan control and temperature sensors; needed for full sensor access and software fan control on the [Hardware](hardware.md) page.
- **Skip** - close the window without installing anything (you can come back later).
- **Set up** - installs the tools you have chosen. Afterwards the button becomes **Retry** if something failed, or **Close** when everything is done.

---

[Back to the page list](../../README.md#pages)
