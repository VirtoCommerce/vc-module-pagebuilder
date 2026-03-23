# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Overview

A **VirtoCommerce platform module** for page builder / theme editor functionality. The repo contains multiple sub-projects across different stacks:

| Sub-project | Stack | Location |
|---|---|---|
| C# backend | .NET 10, EF Core | `src/VirtoCommerce.PageBuilderModule.*` |
| Angular 20 designer | Angular 20, NgRx, Signals | `src/.../Apps/page-builder-designer/` |
| Vue 3 shell | Vue 3, Vite, @vc-shell | `src/.../Apps/page-builder-shell/` |
| AngularJS admin | AngularJS (legacy) | `src/.../Web/Scripts/` |

---

## Commands

### C# / .NET Backend

```bash
# Build solution
dotnet build VirtoCommerce.PageBuilderModule.sln

# Run tests
dotnet test

# Build specific project
dotnet build src/VirtoCommerce.PageBuilderModule.Web/
```

### Angular 20 Designer

All commands run from: `src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/`

```bash
npm start                    # Dev server
npm run watch                # Watch mode (rebuild on changes)
npm run build                # Full build (ngv-markdown → ngv-datepicker → themes → app)
npm run build:datepicker     # Rebuild datepicker library + themes only
ng build                     # Main app only (requires dist/ to have built libraries)
npm test                     # Karma + Jasmine tests
ng test --include='**/path/to/component.spec.ts'  # Single test file
npm run lint                 # ESLint
```

> The internal libraries (`ngv-markdown`, `ngv-datepicker`) must be built before the main app. `npm run build` handles the correct order. `ngv-datepicker` also requires a separate Sass theme compilation step (included in `npm run build:datepicker`).

### Vue 3 Shell

All commands run from: `src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-shell/`

```bash
yarn serve                   # Dev server
yarn build                   # Production build
yarn build:app               # App build (APP_ENV=production)
yarn build:dev               # Dev environment build
yarn type-check              # Vue-tsc type checking
```

---

## Architecture

### C# Module Structure

Standard VirtoCommerce module pattern:

- **`Core/`** — Domain models, service interfaces, events, `ModuleConstants`. No dependencies on Data or Web.
- **`Data/`** — EF Core DbContext, entity models, service implementations, authorization handlers, search indexing. Depends on Core and platform.
- **`Data.SqlServer/`, `Data.MySql/`, `Data.PostgreSql/`** — Database provider projects. Each contains EF Core migrations for its target DB.
- **`Web/`** — .NET Web SDK project (output is a library, not executable). Contains API controllers, AngularJS scripts, and serves compiled Angular/Vue apps. Depends on Core + Data.
- **`Tests/`** — xUnit v3 tests.

**Version:** 3.1001.0 (defined in `Directory.Build.props`). Platform dependency: VirtoCommerce 3.1000.0+.

**Key C# patterns:**
- `TreatWarningsAsErrors: true` — no suppressed warnings
- Event-driven architecture: `PageBuilderPageChanging/Changed`, `GroupedPageChanging/Changed`
- Content streaming via `ContentStreamRepository` / `IContentStreamProvider`
- Authorization via `PageBuilderAuthorizationHandler` + `PageBuilderAuthorizationRequirement`

### Angular 20 Designer App

Lives entirely in `Apps/page-builder-designer/`. The SPA runs as an `<iframe>` inside the VirtoCommerce platform admin shell. See `Angular Patterns` section below.

**Module structure** (`src/app/modules/`):
- **`core/`** — Services (HTTP, assets, clipboard, modal, notifications, event bus), UI controls, dialogs. Eagerly provided.
- **`editor/`** — Page/template editing. Lazy-loaded at `/pages`. Has its own NgRx store (three-layer: data/domain/ui).
- **`theme/`** — Theme editor. Lazy-loaded at `/themes`. Has its own NgRx store.
- **`shared/`** — Components, dialogs, services, NgRx routing state. Shared across features.
- **`integration/`** — Platform `postMessage`/`BroadcastChannel` integration. `AppInitializator` runs at startup via `APP_INITIALIZER`.
- **`models/`** — Pure TypeScript model definitions.

**Platform integration:**
- Auth JWT read from `localStorage` (`ls.authenticationData`) — set by VC platform shell
- Communicates with parent via `postMessage` and `BroadcastChannel` (`vc-module-content-channel`)
- Preview iframe receives template data changes in real-time

**Config system:**
- Loads `data/settings.json` (or `?configUrl=` override)
- Supports `{{token}}` template syntax in URLs (tokens: `location.*`, `config.*`, `settings.*`)
- `BuilderHttpClient` evaluates `ServerRequestDescriptor` objects with LRU cache + fallback chains

**Internal libraries (`projects/`):**
- **`ngv-datepicker`** — Custom datepicker with clock view. Has prebuilt Sass themes (compiled to `.dist-libs/`).
- **`ngv-markdown`** — Markdown editor wrapping EasyMDE + Turndown (HTML→Markdown paste).

### Vue 3 Shell App

Lives in `Apps/page-builder-shell/`. Built with Vite 6 and uses the `@vc-shell/framework` for VirtoCommerce module shell integration. Uses Vue 3 Composition API, `vue-router`, `vee-validate` for forms, and auto-generated API client from `@vc-shell/api-client-generator`.

### AngularJS Admin Integration

Located at `Web/Scripts/`. Module registered as `virtoCommerce.pageBuilderModule`. Provides:
- Page editing blade (`blades/pages/edit-page.js`)
- Store detail widget (`widgets/pageBuilder-app-widget.js`)
- API resource (`resources/pageBuilderApi.js`)
- File upload handler (`services/page-builder-file.handler.js`)

This is **legacy code** — do not add new features here. It integrates with the VirtoCommerce admin blade architecture.

---

## Angular 20 Patterns (applied throughout `page-builder-designer`)

### Dependency Injection
Always use `inject()` — never constructor injection.
Exception: subclasses of framework classes must call `super(inject(X))`:
```typescript
private readonly store = inject(Store<BuilderState>);
private readonly destroyRef = inject(DestroyRef);

// For subclasses:
constructor() { super(inject(HttpHandler)); }
```

### Component Inputs / Outputs / Queries
```typescript
// Signal-based inputs (simple values, no side effects)
readonly label = input.required<string>();
readonly opened = input(false);

// Getter/setter @Input — keep when side effects needed (e.g. generateForm)
@Input({ required: true }) set descriptor(value: ...) { this.generateForm(value); }

// Mutable input (externally bound AND internally mutated)
readonly controlValue = signal<any>(null);
@Input('controlValue') set controlValueInput(v: any) { this.controlValue.set(v ?? null); }

// output() replaces @Output() + EventEmitter
readonly onAdd = output<SectionItem>();

// viewChild() replaces @ViewChild
readonly frame = viewChild<ElementRef>('frame');
readonly host = viewChild.required(ControlHostDirective);
```

### Control Flow
Use `@if` / `@for` / `@switch` — **never** `*ngIf` / `*ngFor` / `[ngSwitch]`:
```html
@if (viewModel(); as vm) { ... }
@for (item of items(); track item.id) { ... }
```
Guard `[formGroup]="form"` with `@if (form)` when form is async.

### Signals & NgRx
```typescript
// NgRx selector → signal (no async pipe needed)
readonly viewModel = toSignal(this.store.select(selectSomething), { initialValue: null });
// Local state
readonly isOpen = signal(false);
// Keep Subject + debounceTime for time-based streams
```
No `AsyncPipe` anywhere.

### Lifecycle / Cleanup
No `ngOnDestroy`. Use `takeUntilDestroyed()` + `DestroyRef.onDestroy()`:
```typescript
private readonly destroyRef = inject(DestroyRef);
this.destroyRef.onDestroy(() => clearInterval(this._interval));
someStream$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(...);
```

### NgRx Effects — CRITICAL: inject() fields BEFORE createEffect fields
Due to `useDefineForClassFields: false`, inject fields must come before `createEffect` fields or services will be `undefined` at init time.

### Path Aliases (`tsconfig.json`)
| Alias | Maps to |
|---|---|
| `@app/*` | `src/app/*` |
| `@core/*` | `src/app/modules/core/*` |
| `@shared/*` | `src/app/modules/shared/*` |
| `@editor/*` | `src/app/modules/editor/*` |
| `@theme/*` | `src/app/modules/theme/*` |
| `@integration/*` | `src/app/modules/integration/*` |
| `@models/*` | `src/app/modules/models/*` |

### Angular Material MDC Overrides
```scss
::ng-deep .mdc-tab__text-label {
    font-size: $default-font-size;
    font-weight: 400;
    letter-spacing: normal;
}
```
`mat-button` wraps content in `.mdc-button__label` — wrap inner content in `<span>` for custom flex.

### ng-scrollbar
```scss
.parent-container {
    flex: 1 1 0;
    overflow: hidden;
    ng-scrollbar { height: 100%; }
}
```
`visibility` accepts `'native' | 'hover' | 'visible'` (not `'always'`).

### Lazy Controls
Heavy controls (CKEditor, calendar, color, markdown, files, images) are registered lazily via dynamic `import()` in `controls-register.ts`. `ControlHolderComponent` buffers `writeValue`/`registerOnChange`/`registerOnTouched` until the component is created.

### @defer
Use `@defer (on viewport; prefetch on idle)` for large visual lists. Not for `ViewContainerRef.createComponent()` patterns.

---

## CI/CD

- `.github/workflows/module-ci.yml` — main pipeline (Node 20, build + package NuGet)
- `.github/workflows/deploy.yml` — deployment
- `.github/workflows/publish-nugets.yml` — NuGet publishing
- `Jenkinsfile` — Jenkins pipeline
- Build output: `dist/template-builder/` — deployed as part of the .NET module
