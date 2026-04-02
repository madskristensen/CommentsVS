# Issue Links

Comment Studio makes issue references like `#123` in comments clickable, linking directly to the issue on your hosting provider. No more copying issue numbers and searching manually.

## Supported Providers

The extension detects the Git remote URL and generates the correct link automatically:

| Provider | Example Link |
|----------|-------------|
| GitHub | `https://github.com/owner/repo/issues/123` |
| GitLab | `https://gitlab.com/owner/repo/-/issues/123` |
| Bitbucket | `https://bitbucket.org/owner/repo/issues/123` |
| Azure DevOps | `https://dev.azure.com/org/project/_workitems/edit/123` |

## Usage

- **Ctrl+Click** on any `#123` reference in a comment to open the issue in your browser.
- **Hover** over the reference to see a tooltip with the full URL.

![Issue Links](../art/issue-links.png)

## Related

- [Settings — Issue Links](settings.md#issue-links)
- [Comment Tags — Tag Metadata](comment-tags.md#tag-metadata) (for `#issue` references in tag metadata)
