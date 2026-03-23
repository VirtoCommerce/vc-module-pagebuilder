# Design System

## Description
Styling conventions, theme configuration, and UI component library used in the page builder. Use this skill when creating UI, styling components, or choosing the right Material/Tailwind approach.

## Triggers
- Creating or modifying component styles (SCSS)
- Adding UI elements (buttons, dialogs, forms, icons)
- Working with layout, spacing, responsive design
- Choosing between Tailwind utility classes and SCSS

---

## UI Stack

| Layer | Technology |
|---|---|
| Utility-first CSS | Tailwind CSS 3 (JIT mode) |
| Component library | Angular Material 20 (MDC-based) |
| CDK | Drag-drop, Overlay, Clipboard |
| Scrollbars | ngx-scrollbar |
| Color pickers | ngx-color (Sketch, Twitter variants) |
| File uploads | @iplab/ngx-file-upload |
| Select dropdowns | @ng-select/ng-select |
| Rich text | CKEditor 4 |
| Markdown | Internal ngv-markdown library (EasyMDE) |
| Date picker | Internal ngv-datepicker library |
| Notifications | ngx-toastr |

## Typography

- **Font family:** `'Inter', sans-serif` (set on `body` in `styles.scss`)
- **Custom Material typography:** `mat.define-typography-config($font-family: 'Inter')`

### Font Sizes (SCSS variables in `src/layout/variables.scss`)

| Variable | Value |
|---|---|
| `$default-font-size` | `0.875rem` (14px) |
| `$middle-font-size` | `0.9375rem` (15px) |
| `$small-font-size` | `0.6875rem` (11px) |
| `$icon-size` | `1rem` (16px) |
| `$small-icon-size` | `0.75rem` (12px) |
| `$btn-icon-size` | `0.8125rem` (13px) |

## Color Palette (SCSS variables)

### Text Colors
| Variable | Hex | Usage |
|---|---|---|
| `$heading-color` | `#121212` | Headings |
| `$content-color` | `#202223` | Content text |
| `$text-color` | `#363636` | General text |
| `$controls-text-color` | `#363636` | Dropdowns, toolbar sections |
| `$label-color` | `#646464` | Form labels |
| `$info-color` | `#6D7175` | Info/helper text |
| `$disable-text-color` | `#8C9196` | Disabled text |

### UI Colors
| Variable | Hex | Usage |
|---|---|---|
| `$active-color` | `#1665D8` | Active/selected state |
| `$icons-color` | `#484848` | Icons |
| `$disable-icon-color` | `#8C9196` | Disabled icons |
| `$secondary-element-color` | `rgba(72, 72, 72, 0.48)` | Secondary elements |
| `$hover-color` | `rgba(246, 246, 246, 1)` | Hover backgrounds |

### Form Colors
| Variable | Hex | Usage |
|---|---|---|
| `$form-controls-text-color` | `#202223` | Form input text |
| `$controls-border-color` | `#BABFC3` | Input borders |
| `$border-color` | `#e0e0e0` | General borders |
| `$no-image` | `#BABFC3` | Image placeholder |

## Material Theme

- Base theme: `indigo-pink` prebuilt (imported directly as CSS)
- Custom typography override with Inter font via `mat.all-component-typographies`
- CDK overlay styles imported

**Note:** Theme colors are NOT customized beyond defaults — the codebase has TODO comments about properly redefining Material palette colors.

## Angular Material — MDC Notes

All Angular Material components are MDC-based. This changes internal DOM structure vs the pre-MDC components.

### mat-button Internal Structure
In MDC, `mat-button` wraps projected content in `<span class="mdc-button__label">`. This breaks any flex layout that relied on direct children of the button being flex items.

**Wrong approach** — relying on `flex` on the button itself:
```scss
// ❌ All children end up inside one .mdc-button__label span
.my-button {
    @apply flex flex-row;
}
```

**Correct approach** — wrap content in our own span:
```html
<!-- template -->
<button mat-button class="my-button">
    <span class="btn-inner">
        <app-icon>...</app-icon>
        <span class="label">Text</span>
        <app-chevron></app-chevron>
    </span>
</button>
```
```scss
// scss — we own the layout, not Material
.my-button {
    @apply p-2 w-full;
    font-size: $default-font-size;
    font-weight: 400;
    letter-spacing: normal;
}
.btn-inner {
    @apply flex flex-row items-center w-full min-w-0;
}
```

This same principle applies to `mat-menu-item` — wrap content in a `.item-inner` span if you need custom flex layout inside it.

### mat-button Typography Override
MDC `mat-button` applies its own typography tokens (`font-weight: 500`, `letter-spacing: 0.00625em`). To match the project's design, explicitly reset these on custom-styled buttons:
```scss
.my-button {
    font-size: $default-font-size;
    font-weight: 400;
    letter-spacing: normal;
}
```

### mat-menu Panel Padding
MDC menu has default vertical padding in `.mat-mdc-menu-content`. Override at the component level:
```scss
::ng-deep .my-panel .mat-mdc-menu-content {
    padding: 0 !important;
}
```

### Material Icons
In Angular Material 16, the `mat-icon` component no longer guarantees the correct font-family. Added explicit CSS in `src/layout/material-theme.scss`:
```scss
.material-icons {
    font-family: 'Material Icons' !important;
    font-feature-settings: 'liga';
    -webkit-font-feature-settings: 'liga';
}
```

### Standalone Imports
Material modules are no longer imported in a central NgModule. Each standalone component imports only what it needs:
```typescript
@Component({
    standalone: true,
    imports: [MatButtonModule, MatMenuModule, MatIconModule]
})
```

## Tailwind CSS Configuration

- **Mode:** JIT
- **Content:** `./src/**/*.html`, `./src/**/*.scss`
- **Plugins:** `@tailwindcss/forms`, `@tailwindcss/typography`
- **No custom theme extensions** — uses default Tailwind values
- Integrated via custom webpack + PostCSS

## Styling Approach

### When to use Tailwind vs SCSS
- **Tailwind** for layout, spacing, positioning (via `@apply` in component SCSS or directly in templates)
- **SCSS** for component-specific styles, variables, Material overrides
- **SCSS variables** (not CSS custom properties) for the color/typography design tokens

### Global Style Files (`src/layout/`)
| File | Purpose |
|---|---|
| `variables.scss` | Design tokens: colors, font sizes, icon sizes |
| `material-theme.scss` | Material typography + theme setup, Material Icons fix |
| `external.scss` | Overrides for third-party component styles |
| `toastr.scss` | Toast notification style overrides |
| `controls.scss` | Shared form control styles |
| `dialogs.scss` | Dialog/modal styles |
| `extras.scss` | Misc global utility styles |

### Common Layout Pattern
Overlap panels use a standard Tailwind composition:
```scss
.overlap-panel {
    @apply absolute h-full bg-white inset-y-0 left-8 -right-8 flex flex-col;
}
```

## No Dark Mode
Dark mode is not implemented. The `tailwind.config.js` has `darkMode` commented out.

## No Custom Breakpoints
Uses default Tailwind breakpoints. No custom responsive configuration.
