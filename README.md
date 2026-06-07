# cxshell

<div align="center">
  <img src=".github/logo.svg" alt="cxshell Logo" width="600">
</div>

<div align="center">

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Linux%20|%20macOS%20|%20Windows-orange.svg)]()

</div>

**A terminal desktop shell built on [SharpConsoleUI](https://github.com/nickprotop/ConsoleEx).**

<div align="center">

### ⭐ If you find cxshell useful, please consider giving it a star! ⭐

It helps others discover the project and motivates continued development.

[![GitHub stars](https://img.shields.io/github/stars/nickprotop/cxshell?style=for-the-badge&logo=github&color=yellow)](https://github.com/nickprotop/cxshell/stargazers)

</div>

cxshell is a complete desktop environment that lives entirely in the terminal: a windowed
shell with a Start menu, taskbar, system tray, and built-in apps — a File Manager, Settings,
a Terminal, and an App Manager — all rendered with truecolor and mouse support. It runs
standalone over SSH as a full-screen TUI "operating system" feel, and is the desktop shell
that powers [DotOS](https://github.com/nickprotop/dotos).

**Login. Launch. Manage.**

## Quick Start

**Option 1: One-line install** (Linux/macOS, no .NET required)
```bash
curl -fsSL https://raw.githubusercontent.com/nickprotop/cxshell/main/install.sh | bash
cxshell
```

**Option 2: Build from source** (requires .NET 10)
```bash
git clone https://github.com/nickprotop/cxshell.git
cd cxshell
dotnet run --project src/cxshell
```

## Use cases

- **Standalone / SSH TUI shell** — run `cxshell` on a server and get a full windowed desktop
  in your terminal session.
- **DotOS desktop** — DotOS builds cxshell from this repo (as a sibling checkout) and boots
  directly into it.

## Apps & the manifest model

cxshell is a *shell*, not a monolith: it launches apps described by freedesktop `.desktop`
manifests, discovered from `/usr/share/cxshell/apps` (system) and
`~/.local/share/cxshell/apps` (user). The built-in apps are separate projects
(`cxshell.FileManager`, `cxshell.Settings`, `cxshell.Terminal`, `cxshell.AppManager`); the
**App Manager** installs/updates/removes additional apps at runtime (binary, source-build,
and publisher-script installs).

## Architecture

| Project | Role |
|---------|------|
| `cxshell` | The shell executable: desktop, Start menu, taskbar, system tray |
| `cxshell.Apps` | App infrastructure: manifest store, install manager, installers, sandbox |
| `cxshell.FileManager` / `.Settings` / `.Terminal` / `.AppManager` | Built-in apps (also run standalone) |

Built on **ConsoleEx (SharpConsoleUI)** — a retained-mode terminal UI framework. The
dependency resolves to the local `../ConsoleEx` sibling project when present (development),
otherwise the `SharpConsoleUI` NuGet package (CI/release).

### Gotcha — launch via the apphost

Launch the shell via its native apphost binary (`cxshell`), never `dotnet cxshell.dll`. The
embedded Terminal's PtyShim re-execs `Environment.ProcessPath`; under the `dotnet` muxer that
becomes `dotnet --pty-shim …` (rejected, garbling the first terminal). The apphost makes
`ProcessPath` the `cxshell` binary so the re-exec is correct.

## Development

```bash
dotnet build cxshell.slnx -c Release   # build all projects
dotnet test                            # run the test suite
dotnet run --project src/cxshell       # run the shell
```

## Releasing

`./publish.sh [patch|minor|major]` bumps the version, tags `v*`, and pushes — GitHub Actions
builds `cxshell-linux-{x64,arm64}` (plus macOS/Windows) self-contained single-file binaries
with `SHA256SUMS` and creates the release.

## License

MIT © Nikolaos Protopapas
