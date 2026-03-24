# Code Review: Angular 21 Patterns — page-builder-designer

## Context

Angular 21 upgrade is complete (dependencies installed, build passes). This plan covers the **code review** pass — finding remaining legacy patterns in the codebase and bringing them in line with Angular 21 best practices.

The upgrade from Angular 20 → 21 already handled:
- All `@HostBinding`/`@HostListener` → `host: {}` in 6 components
- `enableProdMode()` removed from `main.ts`
- NgModule-based providers in `ngv-datepicker` → `provide*()` functions
- `ChangeDetectorRef` removed from `color.component.ts` and `search.component.ts`
- NgRx `StoreConfig` import fixed

---

## Findings by category

### 1. `importProvidersFrom` → standalone provider API

**File:** [src/app/app.config.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/src/app/app.config.ts) — line 62

```typescript
// Current:
importProvidersFrom(
    MatDialogModule,
    ToastrModule.forRoot()
)

// Replace ToastrModule.forRoot() with (ngx-toastr v19+):
provideToastr()
// Import from: ngx-toastr

// MatDialogModule — leave with importProvidersFrom (no provideMatDialog() in v21)
```

### 2. Missing `ChangeDetectionStrategy.OnPush`

Confirmed missing in these components:

| File | Notes |
|---|---|
| [overlap-panel.component.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/src/app/modules/core/components/overlap-panel/overlap-panel.component.ts) | Also needs signals + afterNextRender (see §3) |
| [panel.component.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/src/app/modules/core/components/panel/panel.component.ts) | Also needs signal + afterNextRender (see §3) |
| [live-preview.component.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/src/app/modules/shared/components/live-preview/live-preview.component.ts) | Also needs ngOnInit removal (see §4) |
| [controls/select/select.component.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/src/app/modules/core/controls/select) | No state issues, just missing OnPush |
| [controls/collection/collection.component.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/src/app/modules/core/controls/collection) | No state issues, just missing OnPush |

### 3. `ChangeDetectorRef` + `ngAfterViewInit` → signals + `afterNextRender`

**`overlap-panel.component.ts`**:
- `contentWidth: number | null = null` and `expanderPosition: number | null = null` → `signal<number | null>(null)`
- `isOpened = false` → `signal(false)`
- `ngAfterViewInit()` starts `setInterval(() => changeWidth(), 1000)` → move to constructor wrapped in `afterNextRender()`
- `cdr.detectChanges()` inside `changeWidth()` → remove (signals update view automatically with OnPush)
- Remove `ChangeDetectorRef`, `AfterViewInit`

**`panel.component.ts`**:
- `hasFooter = true` → `signal(true)`
- `ngAfterViewInit()` checks DOM children count then calls `detectChanges()` → move to constructor as `afterNextRender()`
- Remove `ChangeDetectorRef`, `AfterViewInit`

Template note: these components use `contentWidth`, `expanderPosition`, `hasFooter`, `isOpened` in their templates — signal reads will need `()` where they don't already have it.

### 4. `ngOnInit` → constructor

**`live-preview.component.ts`**:
- `ngOnInit()` subscribes to event bus, sets `url` and `previewUrl` from config
- All dependencies injected via `inject()` — available from constructor immediately
- Move entire body to constructor, remove `ngOnInit()` and `OnInit` interface
- `previewUrl!: SafeResourceUrl` → `previewUrl: SafeResourceUrl` (initialized in constructor)

### 5. `@HostListener` remaining in `ngv-datepicker`

**File:** [projects/ngv-datepicker/src/lib/clock-view.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/projects/ngv-datepicker/src/lib/clock-view.ts) — line 8, ~146

```typescript
// Current: @HostListener('window:resize') onClockResize() { ... }
// Migrate to host: { '(window:resize)': 'onClockResize()' }
// Remove HostListener import
```

---

## Leave as-is (justified)

| Pattern | Location | Reason |
|---|---|---|
| `@Input()` getter/setters | `control-holder.component.ts` | Side effects: propagates value to dynamic component. CLAUDE.md exception. |
| `cdr.detectChanges()` | `control-holder.component.ts` | After `ViewContainerRef.createComponent()` — Angular requires explicit CD trigger here |
| `ngOnInit` | `control-holder.component.ts` | Needs `descriptor()` signal value after inputs resolved |
| `@Input()` + `ChangeDetectorRef` + lifecycle hooks | All `ngv-datepicker` internals (calendar, month-view, year-view, etc.) | Forked Angular Material component — `stateChanges` Subject and CDK form field integration require these patterns |
| `Subject<void>` for stream coordination | `collection.component.ts`, `object.component.ts`, `base-files.component.ts` | Drive `merge()`/`switchMap()` reactive chains — appropriate use of RxJS |
| `HTTP_INTERCEPTORS` class-based interceptor | `app.config.ts` + `refresh-token.interceptor.ts` | Migration to functional interceptors is a separate refactoring task |

---

## Execution order

1. `app.config.ts` — replace `ToastrModule.forRoot()` with `provideToastr()`
2. `overlap-panel.component.ts` — signals + `afterNextRender` + OnPush + remove CDR
3. `panel.component.ts` — signal + `afterNextRender` + OnPush + remove CDR
4. `live-preview.component.ts` — move `ngOnInit` body to constructor + OnPush
5. `select.component.ts`, `collection.component.ts` — add `ChangeDetectionStrategy.OnPush`
6. `clock-view.ts` — migrate `@HostListener` to `host: {}`
7. `npm run build` — verify no errors
8. `npm test` — verify all tests pass

## Verification

- `npm run build` completes without errors
- `npm test` — all 13 main app tests pass, ngv-markdown test passes
- In the running app: overlap-panel expand/collapse still works, panel footer detection still works, live-preview loads and receives messages, datepicker clock view still resizes correctly
