[marketplace]: https://marketplace.visualstudio.com/items?itemName=MadsKristensen.CommentsVS
[vsixgallery]: http://vsixgallery.com/extension/CommentsVS.38981599-e7f2-4db8-bf34-85b325aec2b6/
[repo]: https://github.com/madskristensen/CommentsVS

# Modern Comments for Visual Studio

[![Build](https://github.com/madskristensen/CommentsVS/actions/workflows/build.yaml/badge.svg)](https://github.com/madskristensen/CommentsVS/actions/workflows/build.yaml)

Download this extension from the [Visual Studio Marketplace][marketplace]
or get the [CI build][vsixgallery].

--------------------------------------

A Visual Studio extension that brings a modern commenting experience with automatic XML documentation comment reformatting. Keep your documentation clean, readable, and consistently formatted.

## Features

### Automatic Comment Reflow
Automatically reformat XML documentation comments to fit within a configurable line length (default: 120 characters). The extension intelligently wraps text while preserving XML structure.

### Format Document Integration
When you use **Format Document** (Ctrl+K, Ctrl+D) or **Format Selection** (Ctrl+K, Ctrl+F), all XML documentation comments in scope are automatically reflowed to your configured line length.

### Smart Paste
Paste text into an XML documentation comment, and the extension automatically reflows the entire comment block to maintain proper formatting.

### Light Bulb Action
Place your cursor inside any XML documentation comment and press **Ctrl+.** to see the "Reflow XML Documentation Comment" action.

![Light Bulb](art/lightbulb.png)

### Style C Formatting
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
Works with C#, VB.NET, and C++ XML documentation comments:
- `///` single-line comments (C#, C++)
- `''' ` single-line comments (VB.NET)
- `/** */` block comments

## Configuration

Go to **Tools → Options → Comments** to configure:

| Option | Default | Description |
|--------|---------|-------------|
| Max Line Length | 120 | Maximum line length for reflowed comments |
| Reflow on Format Document | true | Enable reflow when formatting document/selection |
| Reflow on Paste | true | Enable reflow when pasting into comments |
| Use Compact Style | true | Use single-line format for short summaries |
| Preserve Blank Lines | true | Keep intentional blank lines in comments |

## Getting Started

1. Install the extension from the Visual Studio Marketplace
2. Open any C#, VB.NET, or C++ file with XML documentation comments
3. Use any of the following to reflow comments:
   - **Format Document** (Ctrl+K, Ctrl+D)
   - **Format Selection** (Ctrl+K, Ctrl+F)
   - **Light Bulb** (Ctrl+.) → "Reflow XML Documentation Comment"
   - **Paste** text into a comment block

## Requirements

- Visual Studio 2022 (17.0 or later)
- Supports both x64 and ARM64 architectures

## How can I help?

If you enjoy using the extension, please give it a ★★★★★ rating on the [Visual Studio Marketplace][marketplace].

Should you encounter bugs or have feature requests, head over to the [GitHub repo][repo] to open an issue if one doesn't already exist.

Pull requests are also very welcome, as I can't always get around to fixing all bugs myself. This is a personal passion project, so my time is limited.

Another way to help out is to [sponsor me on GitHub](https://github.com/sponsors/madskristensen).
