# Dashboard

**Where to find it:** Overview (the first item in the left menu; this category has just this one page, so there are no tabs).

The Dashboard answers "how is this PC doing right now?" at a glance. Everything updates live while the page is open, and it is the first page you see when Porchlight starts.

![Dashboard showing live CPU, memory, GPU, disk and network tiles, drive space bars with a low-space warning, and a list of the busiest programs](../screenshots/dashboard.png)

*(Screenshot uses made-up demo data, not a real PC.)*

## Top of the page

- **Subtitle line** - the computer's name, the version of Windows, and how long it has been on since the last restart ("up 3d 0h 0m").
- **Restart pending** (yellow badge, top right) - only appears when Windows is waiting for a restart to finish installing something. Restarting clears it.

## Live tiles

Six tiles, each with a big number and a small history graph that fills in as time passes. Hover over a tile for a short explanation.

- **CPU** - how busy the processor is, as a percentage, and how many "logical processors" it has.
- **Memory** - percentage of memory (RAM) in use, with used and total gigabytes.
- **GPU** - how busy the graphics chip is, with the graphics card's name. If the PC cannot report this, the tile says so instead of showing a number.
- **Disk** - how busy the disks are, with the current read and write speeds.
- **Download** - current incoming internet speed, and the total received since Porchlight started.
- **Upload** - current outgoing internet speed, and the total sent since Porchlight started.

## Drives

One row per drive (C:, D:, ...) with a bar showing how full it is and how much space is free out of the total. A drive that is nearly full gets a **Low space** warning and a **Free up space** button, which takes you to the [Free up space](free-up-space.md) page.

## Top processes

The programs currently using the most processor time, with each one's CPU percentage and memory use. Programs with several parts (like a web browser) are grouped into one line with a count, e.g. "(6)".

## System

A short fact sheet about the PC: computer name, operating system, manufacturer and model, processor, number of cores, graphics card, and installed memory. Handy to read out over the phone when someone is helping you.

---

[Back to the page list](../../README.md#pages)
