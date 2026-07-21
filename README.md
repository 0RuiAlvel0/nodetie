# NodeTie

NodeTie is a Windows tray application for creating and opening deep links to local files and URLs.

The project is conceptually similar to Hooksmark, but the implementation is Windows-specific and built around a custom `winlink://` URI scheme.

## Problem Scope

Typical file links are path-based. If a file is moved, links can break.

NodeTie addresses this by encoding two values in generated links:

1. The current path
2. A stable file identifier (`sid`)

When a deep link is opened, NodeTie can resolve by path first, then fall back to stable-ID-based lookup for moved files on the same volume.

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
