# Web console

**Where to find it:** Get help > Web console.

Lets you watch this PC's stats from a browser on your phone or another computer. The web console is **read-only**: it only shows information and cannot change anything on this PC. It is off until you turn it on.

## What's on the page

- **Turn on the web console** - the main switch, with a status line saying whether it is running and where.
- **Open this on your other device** - the link to type or send to your phone: **Copy link** and **Open on this PC**. If the link does not open, alternative addresses are listed, each with a **Copy** button. The first time, Windows may ask whether Porchlight can use your network - choose Allow for private networks.
- **Access key** - a secret built into the link. A browser without the key sees nothing. **Copy key** copies it; **Make a new key** creates a fresh one, which locks out every device that has the old link.
- **Port** - the network "door number" the console uses, with a hint about the default. Type a new number and press **Apply** if the default is busy.
- **Keep it private** - a reminder that anyone with the link can see this PC's stats (though never change anything). Use it on your home network; to check in from elsewhere, use a VPN such as Tailscale rather than opening the port on your router.

## What the console shows

Open the link on your phone or another computer and you will see the live tiles for CPU, memory, GPU,
disk and network, and a small menu at the top that jumps to each section: **Overview**, **Security**,
**Updates** and **Startup**. It only shows information.

- **Security** - the same headline as the [Safety](safety.md) page, with antivirus and firewall, Windows
  Update, and any remote-control programs found.
- **Updates waiting** - the app updates found at the last check in Porchlight. It does not run a new check
  from the console; if it says Porchlight has not checked yet, open [Updates](updates.md) on the PC.
- **Startup impact** - programs that start with Windows, on or off, slowest first, with the High / Medium
  / Low impact from [Startup apps](startup-apps.md). The impact can only be measured when Porchlight runs
  as administrator on the PC.

None of these views has a button that changes anything.

---

[Back to the page list](../../README.md#pages)
