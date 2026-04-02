# Code Anchors

The Code Anchors tool window provides a centralized view of all comment tags — not just in open files, but across every file in your solution.

Open it via **View > Other Windows > Code Anchors**.

![Code Anchors](../art/code-anchors.png)

## Features

- **Solution-wide scanning** — Finds anchors in all files across your entire solution, not just open documents
- **Background indexing** — Scans files on a background thread without blocking the UI
- **Configurable file types** — Choose which file extensions to scan (C#, TypeScript, HTML, and 30+ more by default)
- **Smart folder exclusions** — Automatically ignores `node_modules`, `bin`, `obj`, `.git`, and other non-source folders
- **Auto-refresh on save** — Updates automatically when you save files in Visual Studio
- **Color-coded indicators** — Each anchor type has a colored circle matching its editor highlight color
- **Quick navigation** — Double-click or press Enter to jump directly to any anchor
- **Keyboard shortcuts** — Use **Alt+Page Down** / **Alt+Page Up** to navigate between anchors
- **Scope filter** — Filter anchors by Solution, Project, Document, or Open Documents
- **Type filter** — Filter by anchor type (TODO, HACK, NOTE, BUG, etc.) using the toolbar combo
- **Built-in search** — Filter anchors by text
- **Group by file** — Organize anchors by their source file for easier navigation
- **Metadata display** — Shows owner (@user), issue references (#123), and anchor IDs
- **Export anchors** — Copy or save anchors to TSV, CSV, Markdown, or JSON formats

## Supported Anchor Types

| Type | Color | Description |
|------|-------|-------------|
| TODO | Orange | Tasks to be completed |
| HACK | Crimson | Temporary workarounds |
| NOTE | Lime Green | Important notes |
| BUG | Red | Known bugs |
| FIXME | Orange Red | Code that needs fixing |
| UNDONE | Purple | Incomplete work |
| REVIEW | Dodger Blue | Code needing review |
| ANCHOR | Cyan | Named navigation points |

## ANCHOR Tags

Use `ANCHOR(name)` to create named navigation points in your code:

```csharp
// ANCHOR(database-init): Database initialization logic starts here
```

These are especially useful for marking important sections you frequently need to return to. They can also be used as targets for [Link Anchors](link-anchors.md).

## Exporting Anchors

Use the **Export** split button in the toolbar to export the currently filtered anchors:

| Format | Best For |
|--------|----------|
| **TSV** | Paste directly into Excel or Google Sheets |
| **CSV** | Import into spreadsheets or databases |
| **Markdown** | Documentation, GitHub issues, wikis |
| **JSON** | Programmatic consumption, CI/CD integration |

The export respects your current filters (scope, type, and search), so you can export exactly the anchors you need. Choose **Export to File...** to save to disk, or use the copy options for quick clipboard access.

## Related

- [Comment Tags](comment-tags.md)
- [Link Anchors](link-anchors.md)
- [Settings — Code Anchors](settings.md#code-anchors)
