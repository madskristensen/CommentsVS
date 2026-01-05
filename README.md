[marketplace]: https://marketplace.visualstudio.com/items?itemName=MadsKristensen.CommentsVS
[vsixgallery]: http://vsixgallery.com/extension/CommentsVS.38981599-e7f2-4db8-bf34-85b325aec2b6/
[repo]: https://github.com/madskristensen/CommentsVS

# Comment Studio for Visual Studio

[![Build](https://github.com/madskristensen/CommentsVS/actions/workflows/build.yaml/badge.svg)](https://github.com/madskristensen/CommentsVS/actions/workflows/build.yaml)
[![Visual Studio Marketplace Version](https://img.shields.io/visual-studio-marketplace/v/MadsKristensen.CommentsVS?label=VS%20Marketplace)][marketplace]
[![Visual Studio Marketplace Downloads](https://img.shields.io/visual-studio-marketplace/d/MadsKristensen.CommentsVS)][marketplace]
[![Visual Studio Marketplace Rating](https://img.shields.io/visual-studio-marketplace/r/MadsKristensen.CommentsVS)][marketplace]

Download this extension from the [Visual Studio Marketplace][marketplace]
or get the [CI build][vsixgallery].

--------------------------------------

**Stop wrestling with XML documentation comments.** Comment Studio transforms how you write, read, and maintain code documentation in Visual Studio.

![Collapsed Comments](art/collapsed.png)

## Why Comment Studio?

XML documentation comments are essential for IntelliSense and API documentation, but they come with frustrations:

- **Hard to read** – XML tags clutter the actual documentation content
- **Tedious to format** – Manual line wrapping and alignment is time-consuming
- **Visual noise** – Long comment blocks obscure the code you're trying to read
- **Easy to miss important notes** – TODO and HACK comments blend into the code

Comment Studio solves all of these problems, letting you focus on writing great documentation instead of fighting with formatting.

## Features

### Comment Rendering Modes
**See your documentation, not the XML.** Choose how you want to view comments:

| Mode | Description |
|------|-------------|
| **Off** | Raw XML syntax with standard Visual Studio coloring |
| **Compact** | Collapse comments to a single line showing just the summary text |
| **Full** | Rich formatted rendering—read documentation like a web page |

Use **Ctrl+M, Ctrl+R** to cycle through rendering modes. The active mode is indicated with a checkmark in the menu.

### Comment Outlining/Collapsing
**Reduce visual clutter instantly.** Collapse XML documentation comments to focus on your code. Expand them only when you need to read or edit.

Use **Ctrl+M, Ctrl+C** to toggle all XML doc comments in the current file between collapsed and expanded states. The extension remembers your preference, so newly opened files will match your last toggle state.

You can also enable **Collapse XML Doc Comments by Default** from the **Edit > Comments** menu (or right-click context menu) to automatically collapse comments when opening files. This option is only available when the rendering mode is set to **Off**.

### Rendered XML Doc Comments
**Read documentation like it was meant to be read.** The Full rendering mode strips away XML noise and displays your documentation with proper formatting:

- **Bold** headings for sections like Remarks, Returns, Parameters
- Clickable links for `<see>`, `<seealso>`, `<paramref>`, and `<typeparamref>` tags
- Inline code formatting for `<c>` and `<code>` blocks  
- Proper list rendering for `<list>` elements
- Gray text color that matches Visual Studio's comment styling (works with light and dark themes)

Use **Ctrl+M, Ctrl+R** to toggle between rendering modes. This is great for reading documentation without the visual clutter of XML tags.

![Rendered Comments](art/rendered-comments.png)

### Context Menu Access
Right-click in any C#, VB, or C++ code editor to access the **Comments** submenu with quick access to:
- **Collapse XML Doc Comments by Default** - Toggle automatic collapsing (only when rendering is Off)
- **Rendering: Off / Compact / Full** - Switch between rendering modes

The same menu is also available from the **Edit** menu.

![Context Menu](art/context-menu.png)

### Automatic Comment Reflow
**Never manually wrap lines again.** The extension automatically reformats XML documentation comments to fit within a configurable line length (default: 120 characters). It intelligently wraps text while preserving XML structure—no more tedious manual formatting.

### Format Document Integration
**Reflow happens automatically.** When you use **Format Document** (Ctrl+K, Ctrl+D) or **Format Selection** (Ctrl+K, Ctrl+F), all XML documentation comments in scope are automatically reflowed to your configured line length.

### Smart Paste
**Paste without breaking formatting.** Paste text into an XML documentation comment, and the extension automatically reflows the entire comment block to maintain proper formatting.

### Auto-Reflow While Typing
**Stay in flow while writing.** As you type in an XML documentation comment, the extension automatically reflows the text when a line exceeds the maximum length. This happens seamlessly with a slight delay (300ms) after you stop typing, ensuring no characters are swallowed.

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

### Color-coded Comment Tags
**Never miss a TODO again.** Comment tags like TODO, HACK, NOTE, BUG, FIXME, UNDONE, and REVIEW are automatically highlighted with distinct colors, making them impossible to overlook. Hover over any tag to see a tooltip explaining its semantic meaning.

| Tag | Default Color | Description |
|-----|---------------|-------------|
| TODO | Orange | Tasks to be completed |
| HACK | Crimson | Temporary workarounds |
| NOTE | Lime Green | Important notes |
| BUG | Red | Known bugs |
| FIXME | Orange Red | Code that needs fixing |
| UNDONE | Purple | Incomplete work |
| REVIEW | Dodger Blue | Code needing review |

Colors can be customized via **Tools > Options > Environment > Fonts and Colors** under "Comment Tag - [TAG]" entries.

![Comment Tags](art/comment-tags.png)

### Clickable Issue Links
**Jump to issues instantly.** Issue references like `#123` in comments automatically become clickable links to the issue on your hosting provider. No more copying issue numbers and searching manually. The extension detects the Git remote URL and supports:

| Provider | Example Link |
|----------|-------------|
| GitHub | `https://github.com/owner/repo/issues/123` |
| GitLab | `https://gitlab.com/owner/repo/-/issues/123` |
| Bitbucket | `https://bitbucket.org/owner/repo/issues/123` |
| Azure DevOps | `https://dev.azure.com/org/project/_workitems/edit/123` |

**Ctrl+Click** on any `#123` reference in a comment to open the issue in your browser. Hover over the reference to see a tooltip with the full URL.

![Issue Links](art/issue-links.png)

## Options

Configure the extension behavior via **Tools > Options > CommentsVS**.

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
| Collapsed by Default | Off | Automatically collapse XML doc comments when opening files (only applies when Rendering Mode is Off or Compact) |

### Comment Rendering
| Setting | Default | Description |
|---------|---------|-------------|
| Rendering Mode | Off | Controls how XML doc comments are displayed: Off (raw XML), Compact (outlining with stripped tags), or Full (rich formatted rendering) |

### Comment Tags
| Setting | Default | Description |
|---------|---------|-------------|
| Enable Comment Tag Highlighting | On | Enable/disable tag highlighting |

Tag colors can be customized via **Tools > Options > Environment > Fonts and Colors** under "Comment Tag - [TAG]" entries.

![Options](art/options.png)

### Issue Links
| Setting | Default | Description |
|---------|---------|-------------|
| Enable Issue Links | On | Make #123 references clickable links to issues |

## Getting Started

1. Install the extension from the Visual Studio Marketplace
2. Open any C# or VB.NET file with XML documentation comments
3. **Try rendering modes** – Press **Ctrl+M, Ctrl+R** to see your comments rendered beautifully
4. **Try reflow** – Edit a comment and use **Format Document** (Ctrl+K, Ctrl+D) to see automatic formatting
5. **Try collapsing** – Press **Ctrl+M, Ctrl+C** to collapse all comments and focus on code

## Requirements

- Visual Studio 2022 (17.0 or later)
- Supports both x64 and ARM64 architectures

## How can I help?

If you enjoy using the extension, please give it a ★★★★★ rating on the [Visual Studio Marketplace][marketplace].

Should you encounter bugs or have feature requests, head over to the [GitHub repo][repo] to open an issue if one doesn't already exist.

Pull requests are also very welcome, as I can't always get around to fixing all bugs myself. This is a personal passion project, so my time is limited.

Another way to help out is to [sponsor me on GitHub](https://github.com/sponsors/madskristensen).
