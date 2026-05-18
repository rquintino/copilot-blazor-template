---
applyTo: "**/*.razor"
---

# Blazor Component Instructions

- Use custom CSS components with theme variables (defined in `wwwroot/css/theme.css`)
- Use CSS classes: `.card`, `.btn-primary`, `.badge`, `.alert-info`, `.data-table`
- No Bootstrap classes — this project uses a custom theme system
- No raw HTML where a styled component class exists
- Use `@rendermode InteractiveServer` for pages with interactivity
- Use `@attribute [Authorize]` for protected pages
- Use `<AuthorizeView>` for conditional rendering based on roles
- Keep components focused and small
- Use file-scoped CSS isolation (`.razor.css`) for component-specific styles
