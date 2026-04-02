# Comment Rendering

Comment Studio transforms how XML documentation comments are displayed in the editor. Instead of reading raw XML tags, you can view your documentation as clean, formatted text.

## Rendering Modes

Choose how XML documentation comments appear in the editor:

| Mode | Description |
|------|-------------|
| **Off** | Raw XML syntax with standard Visual Studio coloring |
| **Compact** | Collapse comments to a single line showing just the summary text |
| **Full** | Rich formatted rendering — read documentation like a web page |

Select your preferred mode from the **Edit > Comments** menu or the right-click context menu. The active mode is indicated with a checkmark.

## Rendered XML Doc Comments

The **Full** rendering mode strips away XML noise and displays your documentation with proper formatting:

- **Bold** headings for sections like Remarks, Returns, Parameters
- Clickable links for `<see>`, `<seealso>`, `<paramref>`, and `<typeparamref>` tags
- Inline code formatting for `<c>` and `<code>` blocks
- Proper list rendering for `<list>` elements
- Customizable colors via **Tools > Options > Environment > Fonts and Colors**

![Rendered Comments](../art/rendered-comments.png)

### Quick Editing

Double-click a rendered comment or press **ESC** when the caret is on a rendered comment line to temporarily switch it to raw source view for editing. Move the caret away from the comment to restore the rendered view.

### Edit Trigger Options

Control how you switch to raw XML editing via **Tools > Options > Comments > Edit Trigger**:

| Option | Description |
|--------|-------------|
| Double-click or Escape | Requires explicit action to edit (default) |
| When caret enters comment | Automatically shows raw XML when the caret moves into the comment |

The "When caret enters comment" option provides a Rider-like editing experience — comments expand automatically as you navigate into them and re-render when you move away.

### Customizing Colors

Colors can be customized via **Tools > Options > Environment > Fonts and Colors**:

| Entry | Description |
|-------|-------------|
| Rendered Comment - Text | Main comment text color |
| Rendered Comment - Heading | Section headings (Returns, Remarks, params) |
| Rendered Comment - Code | Inline code formatting (supports font, background, bold, italic) |
| Rendered Comment - Link | Links, param refs, and type refs |

> See the full [Fonts & Colors reference](fonts-and-colors.md) for all customizable entries.

### Left Border Indicator

A subtle vertical line on the left edge helps distinguish rendered comments from code — similar to Markdown blockquotes. Control when it appears via **Tools > Options > Comments > Left Border**:

| Option | Description |
|--------|-------------|
| Off | No border shown |
| Multiline only | Border on expanded (Full mode) comments only (default) |
| Inline only | Border on compact (single-line) comments only |
| Always | Border on all rendered comments |

## Markdown Formatting in Comments

Comment Studio supports basic Markdown formatting within your XML documentation comments:

| Syntax | Renders As |
|--------|------------|
| `**bold**` or `__bold__` | **bold** |
| `*italic*` or `_italic_` | *italic* |
| `` `code` `` | `code` |
| `~~strikethrough~~` | ~~strikethrough~~ |
| `[text](url)` | clickable link |
| `<https://...>` | auto-link |

Links are clickable and open in your default browser.

You can also use standard XML formatting tags (`<b>`, `<i>`, `<c>`, `<see href="...">`) which are rendered the same way.

### Example

```csharp
/// <summary>
/// Represents a user with *basic* contact information.
/// See the [API docs](https://example.com/api) for more details.
/// Use the **FullUser** class for ~~deprecated~~ `extended` properties.
/// </summary>
```

## Multi-Language Support

Rendering works with C# and VB.NET XML documentation comments:

- `///` single-line comments (C#)
- `'''` single-line comments (VB.NET)
- `/** */` block comments (C#)

## Related

- [Settings — Comment Rendering](settings.md#comment-rendering)
- [Fonts & Colors Reference](fonts-and-colors.md#rendered-comments)
- [Comment Outlining](outlining.md)
