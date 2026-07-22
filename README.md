# NodeTie v.0.0.3 21072026

NodeTie is a Windows tray application for creating and opening deep links to local files and URLs.

The project is conceptually similar to Hooksmark, but the implementation is Windows-specific and built around a custom `winlink://` URI scheme.

Please see the [the project website](https://site.supertechman.com/nodetie) for an updated overview and documentation.

## Problem Scope

Typical file links are path-based. If a file is moved, links can break.

NodeTie addresses this by encoding two values in generated links:

1. The current path
2. A stable file identifier (`sid`)

When a deep link is opened, NodeTie can resolve by path first, then fall back to stable-ID-based lookup for moved files on the same volume.

## NOTES AND TODOs
- Supported applications (you can jump from one of these applications to the other via deep link):
	- Microsoft Edge
	- Mozilla Firefox
	- Microsoft Word
	- Microsoft Excel
	- Windows Explorer (for file selection)
- Add support for the following applications is coming (because I use them/ for others, feel free to contribute):
	- Microsoft PowerPoint
	- Google Chrome
	- Microsoft OneNote  (as in you can jump around through deep links from a file to an OneNote note and back)
	- Obsidian (as in you can jump around through deep links from a file to an obsidian note and back)
	- Microsoft Text Editor.

## Hooksmark Feature Mapping

The table below maps common Hooksmark-style capabilities to NodeTie's current implementation.

| Hooksmark-style capability | NodeTie implementation | Status |
|---|---|---|
| Copy link to current item | Global hotkey resolves active context and copies a `winlink://` deep link | Implemented |
| Durable links after file move | Link includes `sid`; resolver attempts stable-ID relocation | Implemented (same volume) |
| Copy links from file manager selection | Explorer context supports selected-path collection and multi-copy | Implemented |
| Copy link from browser tab | Active browser URL capture and normalization (`http`, `https`, `file`) | Implemented; tested on Microsoft Edge and Firefox |
| Copy link while editing Office document | Foreground-gated Office active context capture | Implemented for Word and Excel |
| Paste-ready note output | Obsidian markdown output and OneNote HTML/plain clipboard output | Implemented |
| Open deep links from notes | `winlink://` protocol registration and decode/open handler | Implemented |
| Related-items panel | Persisted undirected links and linked-files UI | Implemented |
| Bookmarking | Bookmark repository/service + bookmarks window | Implemented |
| Broad app integration matrix | Explorer + browser + Office active-context providers | Partial |

## Current Workflows

### Dynamic links for local files

1. Copy a link for the current file or Explorer selection.
2. Paste into Obsidian or OneNote.
3. Move the file within the same volume.
4. Open the existing deep link.

If path resolution fails, NodeTie attempts stable-ID resolution.

### Browser URLs

NodeTie can capture the active browser tab URL and generate link content for note tools.

Validated browsers: Microsoft Edge and Firefox.

### Office files while in focus

When Word or Excel is the foreground app, NodeTie can capture the active document/workbook path and copy a deep link from that live editing context.

## Limitations

- Windows only.
- Stable-ID relocation is designed for same-volume file moves.
- Office active-context support currently covers Word and Excel; PowerPoint is not implemented in this path.
- Browser capture has explicit validation on Microsoft Edge and Firefox; behavior on other browsers is not guaranteed.
- `winlink://` links require NodeTie protocol registration on the machine opening the link.

## Notes on Intended Usage

- Obsidian mode copies markdown links.
- OneNote mode copies display text plus HTML links.
- Explorer, browser, and Office contexts are treated as link sources; output format is selectable in settings.
- Settings now include a startup toggle: "Launch NodeTie when I sign in to Windows".

## Packaging and Installation (MSI)

NodeTie now includes a WiX v4 installer project under [Installer/NodeTie.Installer.wixproj](Installer/NodeTie.Installer.wixproj).

### What the installer does

- Installs NodeTie under `%ProgramFiles%\NodeTie` (per-machine installation).
- Registers `winlink://` at `HKLM\Software\Classes\winlink` (system-wide protocol registration).
- On first run, app startup config is written at `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\NodeTie`, which makes NodeTie appear under Windows 11 Startup Apps for that user.
- Supports major upgrades via a stable MSI `UpgradeCode`.
- Leaves user data alone on upgrade because the SQLite DB is stored separately at `%LocalAppData%\NodeTie\nodetie.db` by app code.

### Build prerequisites

1. .NET 8 SDK installed.
2. Internet access on first restore so `WixToolset.Sdk` can be restored.

### Build a versioned installer

Set the release version once in `Directory.Build.props`:

```xml
<Project>
	<PropertyGroup>
		<Version>0.0.4</Version>
	</PropertyGroup>
</Project>
```

That shared `Version` value flows into:

- the app executable version shown in About
- the default WiX `ProductVersion`
- the installer build script when `-Version` is omitted

From repo root:

```powershell
pwsh ./scripts/build-installer.ps1
```

To override the shared version for a one-off build:

```powershell
pwsh ./scripts/build-installer.ps1 -Version 1.2.0
```

Artifacts are produced in:

- Publish output: `artifacts/publish/<version>/`
- MSI output: `artifacts/installer/<version>/`

### Versioning and upgrade behavior

Use semantic versioning: `MAJOR.MINOR.PATCH`.

- `PATCH` (e.g., `1.2.0` -> `1.2.1`): bug fix release.
- `MINOR` (e.g., `1.2.1` -> `1.3.0`): backward-compatible feature release.
- `MAJOR` (e.g., `1.9.0` -> `2.0.0`): breaking change release.

For MSI behavior in this repo:

- Build every release with a new `ProductVersion`.
- Keep `UpgradeCode` constant in [Installer/NodeTie.Installer.wixproj](Installer/NodeTie.Installer.wixproj).
- `MajorUpgrade` in [Installer/Product.wxs](Installer/Product.wxs) removes the old MSI and installs the new one.

This means:

- Re-running the same version is effectively a repair/reinstall path.
- Installing a newer version upgrades in place.
- Installing an older version is blocked (downgrade protection).

Practical guidance:

- Even if your app release is a semver patch/minor update, this installer flow still uses MSI major-upgrade mechanics.
- In other words: `1.2.0` -> `1.2.1`, `1.3.0`, and `2.0.0` are all delivered as "install newer MSI over older MSI".
- Database compatibility across those app versions is your responsibility in app startup/migrations; installer behavior is consistent.

### Database preservation

The app database is not installed by MSI and should survive upgrades and uninstall/reinstall cycles as long as `%LocalAppData%\NodeTie\nodetie.db` is retained.

If you later need destructive uninstall behavior, implement it as an explicit opt-in cleanup step, not default MSI behavior.
