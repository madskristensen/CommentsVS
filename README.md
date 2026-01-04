[marketplace]: https://marketplace.visualstudio.com/items?itemName=MadsKristensen.CommentsVS
[vsixgallery]: http://vsixgallery.com/extension/CommentsVS.38981599-e7f2-4db8-bf34-85b325aec2b6/
[repo]: https://github.com/madskristensen/CommentsVS

# Modern Comments for Visual Studio

[![Build](https://github.com/madskristensen/CommentsVS/actions/workflows/build.yaml/badge.svg)](https://github.com/madskristensen/CommentsVS/actions/workflows/build.yaml)

Download this extension from the [Visual Studio Marketplace][marketplace]
or get the [CI build][vsixgallery].

--------------------------------------

A Visual Studio extension that brings a modern commenting experience with automatic XML documentation comment reformatting and collapsing. Keep your documentation clean, readable, and consistently formatted.

## Features

### Comment Outlining/Collapsing
Collapse XML documentation comments to reduce visual clutter and focus on your code. When collapsed, comments display a summary of the content (e.g., `/// Gets the user name...`).

Use **Ctrl+M, Ctrl+C** to toggle all XML doc comments in the current file between collapsed and expanded states. The extension remembers your preference, so newly opened files will match your last toggle state.

![Collapsed Comments](art/collapsed.png)

### Automatic Comment Reflow
Automatically reformat XML documentation comments to fit within a configurable line length (default: 120 characters). The extension intelligently wraps text while preserving XML structure.

### Format Document Integration
When you use **Format Document** (Ctrl+K, Ctrl+D) or **Format Selection** (Ctrl+K, Ctrl+F), all XML documentation comments in scope are automatically reflowed to your configured line length.

### Smart Paste
Paste text into an XML documentation comment, and the extension automatically reflows the entire comment block to maintain proper formatting.

### Auto-Reflow While Typing
As you type in an XML documentation comment, the extension automatically reflows the text when a line exceeds the maximum length. This happens seamlessly with a slight delay (300ms) after you stop typing, ensuring no characters are swallowed.

### Light Bulb Action
Place your cursor inside any XML documentation comment and press **Ctrl+.** to see the "Reflow comment" action.

![Light Bulb](art/lightbulb.png)

### Compact Style Formatting
Short summaries stay on a single line for compactness:
```csharp
/// <summary>Gets the name.</summary>
```

Longer content automatically expands to multi-line format:
```csharp
/// <summary>
/// Gets the full name of the user including first name, middle name,
/// and last name concatenated together.
/// </summary>
```

### Multi-Language Support
Works with C# and VB.NET XML documentation comments:
- `///` single-line comments (C#)
- `'''` single-line comments (VB.NET)
- `/** */` block comments (C#)

## Options

Configure the extension behavior via **Tools > Options > CommentsVS > General**.

### Comment Reflow
| Setting | Default | Description |
|---------|---------|-------------|
| Maximum Line Length | 120 | Maximum line length for reflowed comments |
| Enable Reflow on Format Document | On | Reflow comments when formatting document/selection |
| Enable Reflow on Paste | On | Reflow comments when pasting into comment blocks |
| Enable Reflow While Typing | On | Automatically reflow when line exceeds max length while typing |
| Use Compact Style for Short Summaries | On | Use single-line format for short summaries |
| Preserve Blank Lines | On | Keep intentional blank lines in comments |

### Comment Outlining
| Setting | Default | Description |
|---------|---------|-------------|
| Collapse Comments on File Open | Off | Automatically collapse XML doc comments when opening files |

## Getting Started

1. Install the extension from the Visual Studio Marketplace
2. Open any C# or VB.NET file with XML documentation comments
3. Use any of the following to reflow comments:
   - **Format Document** (Ctrl+K, Ctrl+D)
   - **Format Selection** (Ctrl+K, Ctrl+F)
   - **Light Bulb** (Ctrl+.) → "Reflow XML Documentation Comment"
   - **Paste** text into a comment block
4. Use **Ctrl+M, Ctrl+C** to toggle comment visibility

## Requirements

- Visual Studio 2022 (17.0 or later)
- Supports both x64 and ARM64 architectures

## How can I help?

If you enjoy using the extension, please give it a ★★★★★ rating on the [Visual Studio Marketplace][marketplace].

Should you encounter bugs or have feature requests, head over to the [GitHub repo][repo] to open an issue if one doesn't already exist.

Pull requests are also very welcome, as I can't always get around to fixing all bugs myself. This is a personal passion project, so my time is limited.

Another way to help out is to [sponsor me on GitHub](https://github.com/sponsors/madskristensen).
