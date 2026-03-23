# Angular Patterns

## Description
Conventions and patterns used in this Angular 20 page builder project. Use this skill when creating new components, services, store slices, or modifying existing ones to maintain consistency with the codebase.

## Triggers
- Creating or editing Angular components, services
- Adding NgRx store features (actions, effects, reducers, selectors)
- Working with dynamic forms or control descriptors
- Routing changes
- Writing tests

---

## Angular Version & Features

- **Angular 20** with strict mode (`strict: true`, `strictTemplates: true`, `strictInjectionParameters: true`)
- **All components are standalone** — no NgModules (`bootstrapApplication` entry, feature state at route level)
- **TypeScript** with `noImplicitOverride`, `noPropertyAccessFromIndexSignature`, `noImplicitReturns`
- **`useDefineForClassFields: false`** — set in `tsconfig.json` to prevent ES2022 class field semantics from breaking NgRx effects (services would be `undefined` at field initializer time otherwise)
- **RxJS** — pipe style, no deprecated operators
- Custom webpack via `@angular-builders/custom-webpack` for Tailwind CSS integration

## Component Patterns

### Structure
- Always use separate template/style files (`templateUrl`, `styleUrls`), not inline
- Style language: SCSS
- Prefix: `app-`
- All components are `standalone: true` with explicit `imports: []` array
- `ChangeDetectionStrategy.OnPush` on all components

### Modern Component Template
```typescript
@Component({
    selector: 'app-edit-section',
    templateUrl: './edit-section.component.html',
    styleUrls: ['./edit-section.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [MatButtonModule, SomeChildComponent]  // no NgIf/NgFor/AsyncPipe needed
})
export class EditSectionComponent {
    private readonly store = inject(Store<BuilderState>);
    private readonly destroyRef = inject(DestroyRef);

    // NgRx selector → signal (replaces observable + async pipe)
    readonly viewModel = toSignal(this.store.select(selectEditSectionContext), { initialValue: null });

    onModelChanged(changes: ModelChangedEventArgs) {
        this.store.dispatch(actions.sectionChangedAction({ changes }));
    }
}
```

**Key conventions:**
- No `ngOnInit` or `ngOnDestroy` — use `inject()` + `takeUntilDestroyed(this.destroyRef)` / `DestroyRef.onDestroy()`
- Components are thin — business logic in effects/services
- No `AsyncPipe` — use `toSignal()` instead

## Dependency Injection

**`inject()` function only** — constructor injection is NOT used.

```typescript
private readonly store = inject(Store<BuilderState>);
private readonly destroyRef = inject(DestroyRef);

// Exception: classes extending framework classes must call super
constructor() { super(inject(HttpHandler)); }
```

## Component Inputs / Outputs / Queries

```typescript
// Signal-based inputs (use for simple values — no side effects)
readonly label = input.required<string>();
readonly opened = input(false);

// Getter/setter @Input — keep when side effects are needed (e.g. generateForm)
@Input({ required: true }) set descriptor(value: DescriptorType) { this.generateForm(value); }

// Mutable input (externally bound AND internally mutated)
readonly controlValue = signal<any>(null);
@Input('controlValue') set controlValueInput(v: any) { this.controlValue.set(v ?? null); }

// output() replaces @Output() + EventEmitter
readonly onAdd = output<SectionItem>();

// viewChild() replaces @ViewChild
readonly frame = viewChild<ElementRef>('frame');
readonly host = viewChild.required(ControlHostDirective);
```

## Template Control Flow

Use `@if` / `@for` / `@switch` — **never** `*ngIf` / `*ngFor` / `[ngSwitch]`:

```html
@if (viewModel(); as vm) {
    <app-section [model]="vm.section" />
}
@for (item of items(); track item.id) {
    <app-item [data]="item" />
}
```

Guard `[formGroup]="form"` with `@if (form)` when form is initialized asynchronously.

## Signals & NgRx

```typescript
// NgRx selector → signal
readonly viewModel = toSignal(this.store.select(selectSomething), { initialValue: null });

// Local synchronous state
readonly isOpen = signal(false);

// Observable stream → signal via subscribe
readonly options = signal<any[]>([]);
// in constructor: someStream$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(v => this.options.set(v));

// Keep Subject + debounceTime for time-based streams (search debounce)
```

No `AsyncPipe` anywhere — `toSignal()` or subscribe + signal.

## Lifecycle / Cleanup

No `ngOnDestroy`. Use:
```typescript
private readonly destroyRef = inject(DestroyRef);

// Imperative cleanup
this.destroyRef.onDestroy(() => clearInterval(this._interval));

// RxJS streams
someStream$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(...);
```

## @defer

Use `@defer (on viewport; prefetch on idle)` for large visual lists (e.g. add-section items). Not applicable to `ViewContainerRef.createComponent()` patterns.

## State Management — NgRx

### Bootstrapping
Store wired in `app.config.ts`:
```typescript
provideStore({ router: routerReducer }, { initialState: { router: initialRoute } }),
provideRouterStore({ serializer: RouterSerializer }),
provideEffects([RoutingEffects]),
provideState('shared', sharedReducers),
```

Feature state provided at route level (lazy-loaded):
```typescript
// editor.routes.ts
{
    path: '',
    component: TemplateEditorHostComponent,
    providers: [
        provideState(EditorFeatureName, editorReducers),
        provideEffects(EFFECTS)
    ],
}
```

### Three-Layer Store Pattern (editor & theme)

```
store/
  actions/     data.ts / logic.ts / ui.ts / index.ts
  selectors/   data.ts / domain.ts / ui.ts / common.ts / index.ts
  data/        state.ts / reducers.ts / effects.ts / index.ts
  domain/      state.ts / reducers.ts / effects.ts / index.ts
  ui/          state.ts / reducers.ts / effects.ts / index.ts
  state.ts     (combined BuilderState type)
  index.ts
```

### Action Naming
`[feature name] action description` — with `action` / `actionSuccess` / `actionFails` triplet:

```typescript
export const loadTemplateModel = createAction('[template editor] load template model', props<{ templateKey: string }>());
export const loadTemplateModelSuccess = createAction('[template editor] load template model success', props<{ template: TemplateModel }>());
export const loadTemplateModelFails = createAction('[template editor] load template model fails', props<{ error: HttpErrorResponse }>());
```

### Effects — CRITICAL: inject() fields BEFORE createEffect fields

Due to `useDefineForClassFields: false`, injected services **must be declared before any `createEffect()` fields**:

```typescript
@Injectable()
export class TemplateEditorDataEffects {
    // ✅ inject() fields FIRST
    private readonly store$ = inject(Store<BuilderState>);
    private readonly actions$ = inject(Actions);
    private readonly schemas = inject(SchemasService);

    // ✅ createEffect() fields AFTER
    loadSchemas$ = createEffect(() => this.actions$.pipe(
        ofType(actions.loadTemplateSchemas),
        exhaustMap(() => this.schemas.getSchemas().pipe(
            map(schemas => actions.loadTemplateSchemasSuccess({ schemas })),
            catchError(error => of(actions.loadTemplateSchemasFails({ error })))
        ))
    ));
}
```

- `switchMap` for cancellable, `exhaustMap` for non-cancellable
- `withLatestFrom` to access current state in effects
- Return arrays of actions for multiple dispatches: `switchMap(() => [action1(), action2()])`

### RouterSerializer
Route `data` often carries component class references. `RouterSerializer` filters out functions/classes from route data before NgRx stores them (prevents deep-freeze errors on class constructors):
```typescript
const serializableData = Object.fromEntries(
    Object.entries(data).filter(([, v]) => typeof v !== 'function')
);
```

## Routing

- **Hash-based** (`withHashLocation()`)
- Lazy-loaded via `loadChildren` returning route arrays (not modules)
- Template identity in **query parameters** (`type`, `path`, `groupId`, `parent`)
- Route `data` carries `module` name, `mode` for effects filtering, `toolbar` component class
- **Functional guards** — `CanActivateFn`

## Dynamic Controls — ControlsFactory

Controls registered at startup via `APP_INITIALIZER` to avoid circular webpack dependencies.

```typescript
// controls-register.ts (registered in app.config.ts via APP_INITIALIZER)
export function registerControls(): () => void {
    const factory = inject(ControlsFactory);
    return () => {
        factory.register('calendar', CalendarComponent);  // lazy via dynamic import()
        factory.register('checkbox', CheckboxComponent);  // eager
    };
}
```

`ControlHolderComponent` buffers `writeValue` / `registerOnChange` / `registerOnTouched` calls that arrive before the lazy component is created, then applies them in `createComponent()`.

## Lazy Controls

Heavy controls (text/CKEditor, calendar, color, markdown, files, images) are registered lazily via dynamic `import()`. Light controls are eager. See `controls-register.ts` and `control-holder.component.ts`.

## Forms

- **Reactive Forms** only
- Forms generated dynamically from `BaseControlDescriptor[]` via `formsHelpers.generateForm()`
- `DynamicFormComponent` creates `FormGroup` from descriptors and a `SectionModel`

## Angular Material MDC Style Overrides

MDC components apply non-standard typography (`letter-spacing: 0.089em`). Override at component level:
```scss
::ng-deep .mdc-tab__text-label {
    font-size: $default-font-size;
    font-weight: 400;
    letter-spacing: normal;
}
```

`mat-button` wraps content in `.mdc-button__label` — wrap inner content in your own `<span>` for custom flex layouts.

## ng-scrollbar (ngx-scrollbar v14)

For scroll containment:
```scss
.parent-container {
    flex: 1 1 0;      // flex-shrink must be 1, not 0
    overflow: hidden;
    ng-scrollbar { height: 100%; }
}
```
API: `visibility` type is `'native' | 'hover' | 'visible'`. All inputs are `InputSignal`. Config via `provideScrollbarOptions()`.

## Testing

- **Karma + Jasmine**
- Many tests disabled with `xdescribe` — limited coverage
- Pattern: stub child components with `@Component({ selector, template: '' })`
- Focus tests with `fdescribe`/`fit`
