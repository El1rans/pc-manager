# Running apps

**Where to find it:** Apps & services > Running apps.

"Why is my PC slow?" This page shows what is running right now and how much of the PC each program
uses, and lets you close a program that has stopped responding.

![Running apps page listing programs grouped into Apps and Background, each with CPU and memory use and End task and Open file location buttons](../screenshots/running-apps.png)

*(Screenshot uses made-up demo data, not a real PC.)*

## What's on the page

- **Summary line** - total CPU and memory in use, e.g. "CPU 23% · Memory 9.4 GB of 16 GB in use".
- **Filter** - type part of a program's name to narrow the list.
- **Sort by** - CPU (the default), memory or name.
- The list refreshes every 2 seconds while you are on this page, and stops when you leave it.

## The list

Programs are grouped the way Task Manager does: a program with many parts (a web browser often has
a dozen) is one row, with the number of parts in brackets and their CPU and memory added together.
The rows are in three sections:

- **Apps** - programs with a window open.
- **Background** - programs running without a window (sync tools, updaters, helpers).
- **Windows** - parts of Windows itself. This section starts collapsed.

Each row has:

- **CPU** and **memory** use.
- **Open file location** - opens File Explorer with the program's file selected.
- **End task** - closes the program after asking first ("Unsaved work in it will be lost."). It is
  greyed out for Windows' own programs and for Porchlight itself, because closing those can make
  the PC unstable. Some programs need Porchlight to run as administrator before they can be closed;
  the page says so when that happens.

---

[Back to the page list](../../README.md#pages)
