# Comment Remover

Comment Studio provides several commands to remove comments in bulk, with smart options to preserve important ones.

## Commands

Access these commands via the **Edit > Comments** menu or the right-click context menu:

| Command | Description |
|---------|-------------|
| **Remove All Comments** | Removes all comments from the current document |
| **Remove All Comments in Selection** | Removes comments within the selected text only |
| **Remove All Except XML Doc Comments** | Removes regular comments but preserves `///` documentation |
| **Remove All Except Anchors** | Removes comments but preserves TODO, HACK, NOTE, BUG, etc. |
| **Remove XML Doc Comments Only** | Removes only `///` documentation comments |
| **Remove Anchors Only** | Removes only TODO, HACK, NOTE, BUG, FIXME, UNDONE, REVIEW, and custom tag comments |
| **Remove Regions** | Removes all `#region` and `#endregion` directives |

## Smart Cleanup

When removing comments, entire lines are deleted if they become empty (just whitespace or orphaned comment delimiters like `<!--` or `-->`). This keeps your code clean without leaving behind empty lines.

## Anchor Detection

The "Except Anchors" option recognizes all built-in anchor tags (TODO, HACK, NOTE, BUG, FIXME, UNDONE, REVIEW) plus any [custom tags](comment-tags.md#custom-tags) you've defined in settings.

## Related

- [Comment Tags](comment-tags.md)
- [Code Anchors](code-anchors.md)
