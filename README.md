# cxshell

<div align="center">
  <img src=".github/logo.svg" alt="cxshell Logo" width="600">
</div>

<div align="center">

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Linux%20|%20macOS%20|%20Windows-orange.svg)]()

</div>

**A complete desktop, right in your terminal.** Built on [SharpConsoleUI](https://github.com/nickprotop/ConsoleEx).

<div align="center">

### ⭐ If you find cxshell useful, please consider giving it a star! ⭐

It helps others discover the project and motivates continued development.

[![GitHub stars](https://img.shields.io/github/stars/nickprotop/cxshell?style=for-the-badge&logo=github&color=yellow)](https://github.com/nickprotop/cxshell/stargazers)

</div>

cxshell turns any terminal into a full windowed desktop — a Start menu, taskbar, system tray,
movable windows, mouse support and rich color — with built-in apps for files, settings, a
terminal, and an app store. It's perfect over SSH: log into a remote machine and get a real
desktop experience without a single pixel of X11 or a web browser.

**Log in. Launch. Get things done.**

## Install

**Linux / macOS** — one line, no .NET needed:
```bash
curl -fsSL https://raw.githubusercontent.com/nickprotop/cxshell/main/install.sh | bash
```
Then just run:
```bash
cxshell
```

To remove it later: `cxshell-uninstall.sh`

## What's inside

- **Files** — browse, copy and manage your files in a windowed file manager.
- **Terminal** — a real shell in a window, inside the desktop.
- **Settings** — colors, desktop background, tray, keyboard and mouse options.
- **App Manager** — discover and install more apps with a click.

Open the **Start menu** to launch any of them, switch between windows from the **taskbar**, and
keep an eye on the clock, network and battery in the **system tray**.

## Great over SSH

Because cxshell lives entirely in the terminal, it works anywhere a terminal does. SSH into a
server and run `cxshell` for a full desktop session — windows, menus, mouse and all.

## Build from source

Requires [.NET 10](https://dotnet.microsoft.com/):
```bash
git clone https://github.com/nickprotop/cxshell.git
cd cxshell
dotnet run --project src/cxshell
```

## License

MIT © Nikolaos Protopapas
