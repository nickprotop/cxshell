# DotOS Applications

How applications work in DotOS: the manifest format, where manifests live, how apps are
discovered and launched, and the full lifecycle from discovery to removal.

DotOS is a **desktop shell** (SharpDesk). It does not statically link its apps — it *launches*
them as separate programs, the way a real OS does. Applications are described by **manifests**
(freedesktop `.desktop` files), discovered from well-known directories, and managed
(install / update / remove) by the **App Manager**.

---

## 1. The manifest: a `.desktop` file

A DotOS app manifest is a standard freedesktop **Desktop Entry** (INI syntax), named
`<id>.desktop`, where `<id>` is a reverse-DNS app id (e.g. `org.dotos.cxfiles`). DotOS reads the
standard keys and a few `X-DotOS-*` extensions; any unknown key is ignored, so DotOS manifests
remain valid desktop entries.

```ini
[Desktop Entry]
Type=Application
Name=Files
Comment=Browse, copy and manage files
Exec=/opt/cx/cxfiles %F
Icon=
Categories=System;FileManager;Utility;
Terminal=true
TryExec=/opt/cx/cxfiles
X-DotOS-Group=Applications
X-DotOS-Order=10
X-DotOS-Maximize=false
X-DotOS-Source=binary+github-release:nickprotop/cxfiles?asset=cxfiles-linux-{arch}
X-DotOS-Version=1.4.0
X-DotOS-InstallPath=/opt/cx/cxfiles
```

### Standard keys

| Key | Meaning |
|---|---|
| `Type` | Always `Application`. |
| `Name` | Display name (Start-menu label); localizable as `Name[xx]`. |
| `Exec` | Command line to launch. Field codes `%f %F %u %U` expand to file/URL args (empty by default); `%%` is a literal `%`. Absolute path, or a bare name resolved at launch. |
| `Icon` | A glyph (e.g. a Nerd Font codepoint) or an icon name. |
| `Comment` | Tooltip / subtitle. |
| `Categories` | Standard freedesktop categories (`;`-terminated). Used for interop and as a fallback group mapping. |
| `Terminal` | Informational in DotOS (every app is hosted in a terminal window). |
| `NoDisplay` | `true` hides the entry from the Start menu (still launchable by id). |
| `TryExec` | If set and absent, the entry is treated as not-installed. |

### DotOS extensions (`X-DotOS-*`)

| Key | Meaning |
|---|---|
| `X-DotOS-Group` | Explicit Start-menu section (`Applications` / `System` / `Accessories`). If absent, derived from `Categories`. |
| `X-DotOS-Order` | Sort order within the group (default `0`). |
| `X-DotOS-Maximize` | Launch the host window maximized (default `false` — opens centered). |
| `X-DotOS-Builtin` | `true` for an in-process DotOS app whose `Exec` is a `dotos:builtin/...` pseudo-command. |
| `X-DotOS-Source` | Provenance for install/update (see §4). Present on App-Manager-installed apps. |
| `X-DotOS-Version` | Installed version, compared against the source's latest to detect updates. |
| `X-DotOS-InstallPath` | Where the binary/dir was installed (for clean update/remove). |
| `X-DotOS-Build` | JSON build descriptor for source apps, recorded so updates can rebuild. |

> **A manifest does not install anything.** Launching an entry only runs its `Exec`. Installation
> is a separate, explicit operation performed by the App Manager — never a side effect of launch.

---

## 2. Where manifests live (search & precedence)

Manifests are read from two directories, in increasing precedence:

1. **System (baked, read-only):** `/usr/share/sharpdesk/apps/` — shipped in the image.
2. **User (writable):** `$XDG_DATA_HOME/sharpdesk/apps/` (default `~/.local/share/sharpdesk/apps/`)
   — where the App Manager installs apps, no privilege required.

A manifest in the **user** dir overrides a same-`id` manifest in the **system** dir (whole-file
override). This lets the App Manager upgrade or replace a baked app, or hide one (a user override
with `NoDisplay=true`), without root.

The launcher lists every launchable entry (`Type=Application`, not `NoDisplay`), grouped by
`X-DotOS-Group` (or a `Categories` mapping) and sorted by `X-DotOS-Order` then name.

---

## 3. Launching

When you select an app:

1. The launcher parses `Exec` (field codes dropped, no shell).
2. It resolves the program: an absolute path is used directly; a bare name is searched on `PATH`,
   then `~/.local/bin`, then `/opt/cx`. A `dotos:builtin/...` Exec dispatches to an in-process
   handler.
3. If the program can't be found, you get a "not installed" notification (no broken window).
4. Otherwise it runs as its own process inside a hosted terminal window — maximized when
   `X-DotOS-Maximize` is set. A crash in that app cannot take down the desktop.

---

## 4. Distribution

An app's provenance is its `X-DotOS-Source`, of the form `<kind>+<scheme>:<locator>[?params]`.
DotOS supports **binary** and **source** distribution, and a catalog entry may offer **both**
(you choose at install time).

### Binary

The asset may be a bare executable **or** a compressed archive that is extracted.

| Scheme | Example | Notes |
|---|---|---|
| `binary+url:` | `binary+url:https://example.com/app-{arch}` | **The general case** — any HTTPS URL (your server, S3, GitLab, a CDN). `{arch}`/`{os}`/`{version}` are substituted. |
| `binary+github-release:` | `binary+github-release:owner/repo?asset=app-linux-{arch}` | Convenience: resolves a release asset to a URL, then behaves like `binary+url:`. |
| `binary+apt:` | `binary+apt:package` | Defers to apt (system apps). |

Shared params: `archive=tar.gz|tar.xz|zip` (extract), `exe=<path-in-archive>`, `strip=<n>`,
`sha256=<hex>` (integrity).

### Source (built on-device, **sandboxed**)

| Scheme | Example |
|---|---|
| `source+git:` | `source+git:https://github.com/owner/repo?ref=main` |
| `source+path:` | `source+path:/abs/dir` |

The build pipeline is a **structured descriptor** (so any toolchain works):

```jsonc
"build": {
  "steps": [ { "run": "dotnet publish src/App.csproj -c Release -o out" }, { "script": "post.sh" } ],
  "artifact": "out",   // file or dir produced
  "exe": "App",        // entry executable (within artifact if it's a dir)
  "net": true          // sandbox network (default true, for restore)
}
```

Every source build runs inside a **bubblewrap sandbox**: read-only root, writable only the build
and output dirs, private `/tmp` and PID namespace, network only when `net` is true. The App
Manager shows the full step list before running it, and source installs **fail closed** if no
sandbox is available. (`.NET` SDK and `git`/`bubblewrap` ship in the image.)

---

## 5. The App lifecycle

```
        ┌─────────────┐   browse/search    ┌──────────────┐
        │   Catalog   │ ─────────────────► │  App Manager  │
        │ (embedded / │                    │   (discover)  │
        │   online)   │                    └──────┬───────┘
        └─────────────┘                           │ user picks a source
                                                   ▼
                                          ┌───────────────────┐
                                          │      Install      │
                                          │  binary: download │
                                          │  source: sandbox  │
                                          │         build     │
                                          └────────┬──────────┘
                                                   │ writes ~/.local/share/sharpdesk/apps/<id>.desktop
                                                   ▼
   ┌──────────┐   select    ┌───────────┐   re-scan   ┌──────────────────┐
   │  Launch  │ ◄────────── │ Start menu│ ◄────────── │  Installed (manifest)│
   └──────────┘             └───────────┘             └────────┬──────────┘
                                                   update? │            │ remove
                                                           ▼            ▼
                                                  ┌─────────────┐  ┌──────────────┐
                                                  │   Update    │  │    Remove    │
                                                  │ (same chan- │  │ delete binary│
                                                  │  nel, atomic│  │ + manifest;  │
                                                  │  swap)      │  │ baked → mask │
                                                  └─────────────┘  └──────────────┘
```

1. **Discover** — The App Manager loads a catalog of available apps. Today the catalog is
   **embedded** (a bundled `catalog.json`); it is designed so an **online** repository (same
   schema) can replace it by swapping one provider. Each entry has a name, description (rich HTML
   with logo/screenshots), categories, and one or more sources.
2. **Install** — You pick a source (e.g. *Prebuilt binary* or *Build from source*). Binary:
   download (+ optional extract + checksum). Source: fetch + sandboxed build. The result is placed
   under `~/.local/bin` (or `/opt/cx` for system) and a `.desktop` manifest is written into the
   user apps dir with the resolved version, install path, and (for source) the build descriptor.
3. **Launch** — The app appears in the Start menu and runs as an external process (see §3).
4. **Update** — The App Manager compares the installed `X-DotOS-Version` against the latest on the
   app's recorded channel (release tag for binary, git ref for source). Updating re-installs along
   the same channel to a temp location and **atomically swaps**, keeping one backup.
5. **Remove** — Deletes the binary and the user manifest. A **baked system app** can't be deleted
   (read-only `/usr`); instead the App Manager writes a user override with `NoDisplay=true` to hide
   it.

### Provisioning: baked now, App Manager always

- **Baked at build time:** the image build downloads selected catalog binaries (cxfiles, cxtop)
  into `/opt/cx` and installs their manifests, so the OS ships complete and works offline. The
  App Manager itself is always present (built in-tree, runs from `/opt/sharpdesk`).
- **At runtime:** the App Manager installs additional apps post-boot into the user manifest dir —
  no rebuild, no root. The manifest directory is the single seam both paths share.

---

## 6. Authoring a manifest (quick reference)

Minimum to appear in the Start menu and launch an installed binary:

```ini
[Desktop Entry]
Type=Application
Name=My App
Exec=/opt/cx/myapp
Categories=Utility;
X-DotOS-Group=Applications
```

To make it installable/updatable by the App Manager, add a catalog entry with a `sources[]` list
(binary and/or source). See `src/SharpDesk.AppManager/Catalog/catalog.json` for worked examples
of all five bundled apps.

---

*Normative contract: `docs/superpowers/specs/2026-06-06-app-manifest-and-app-manager-standard.md`.
App Manager design: `…/2026-06-06-app-manager-app-design.md`.*
