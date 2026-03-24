# Refactoring: ControlHolderComponent → NgComponentOutlet

## Context

`ControlHolderComponent` был написан под Angular 13 с использованием `ViewContainerRef.createComponent()` + `ControlHostDirective`. В Angular 16+ появился `ngComponentOutletInputs`, который делает `NgComponentOutlet` полноценным решением для этого паттерна. В Angular 21 это рекомендуемый способ.

**Ответ на вопрос:** да, `NgComponentOutlet` с `ngComponentOutletInputs` — именно то, для чего создан этот случай. Но есть нюанс: `ngComponentOutletInputs` работает только с полями, помеченными `@Input()` или `input()` в целевом компоненте. Сейчас `descriptor`, `currentForm`, `context` в `BaseControlDirective` — обычные публичные свойства без Angular-декораторов, поэтому потребуется небольшое изменение базового класса.

---

## Что меняется и почему

| Было | Стало | Причина |
|---|---|---|
| `ControlHostDirective` + `<ng-template appControlHost />` | `NgComponentOutlet` + `<ng-container>` в шаблоне | `NgComponentOutlet` сам управляет созданием/уничтожением компонента |
| `@Input()` getter/setters для `currentForm`, `context` | `input.required<>()` сигналы | `ngComponentOutletInputs` автоматически пробрасывает значения сигналов |
| `ngOnInit()` + `createComponent()` | `effect()` на `descriptor` | Реактивное создание вместо lifecycle hook |
| `ChangeDetectorRef.detectChanges()` | убирается | `NgComponentOutlet` + сигналы управляют CD сами |
| `viewChild.required(ControlHostDirective)` | `viewChild(NgComponentOutlet)` | Для доступа к `componentRef.instance` (CVA-методы) |
| `OnInit` interface | убирается | — |

---

## Ключевое ограничение: `ngComponentOutletInputs`

Angular передаёт значения из `ngComponentOutletInputs` только в поля, объявленные как `@Input()` или `input()` в целевом компоненте. Без этого свойства будут проигнорированы.

**Изменение в `BaseControlDirective`:**

```typescript
// Было:
public set descriptor(value: T | null) { this._descriptor = value; this.descriptorChanged(); }
context!: ControlContext;
currentForm!: UntypedFormGroup;

// Станет:
@Input() set descriptor(value: T | null) { this._descriptor = value; this.descriptorChanged(); }
@Input() context!: ControlContext;
@Input() currentForm!: UntypedFormGroup;
```

Геттер/сеттер `descriptor` сохраняется — в нём вызывается `descriptorChanged()`, что является side-effect'ом (исключение по CLAUDE.md). `context` и `currentForm` — простые `@Input()`.

---

## Новая реализация `ControlHolderComponent`

```typescript
@Component({
    selector: 'app-control-holder',
    template: `
        @if (currentType(); as type) {
            <ng-container
                [ngComponentOutlet]="type"
                [ngComponentOutletInputs]="componentInputs()">
            </ng-container>
        }
    `,
    providers: [{
        provide: NG_VALUE_ACCESSOR,
        useExisting: forwardRef(() => ControlHolderComponent),
        multi: true,
    }],
    styleUrls: ['./control-holder.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgComponentOutlet]
})
export class ControlHolderComponent implements ControlValueAccessor {

    private readonly controlsFactory = inject(ControlsFactory);

    readonly descriptor = input.required<BaseControlDescriptor>();
    readonly currentForm = input.required<UntypedFormGroup>();
    readonly context = input.required<ControlContext>();

    private readonly outlet = viewChild(NgComponentOutlet);

    readonly currentType = signal<Type<any> | null>(null);

    readonly componentInputs = computed(() => ({
        descriptor: this.descriptor(),
        currentForm: this.currentForm(),
        context: this.context(),
    }));

    // Pending CVA state — buffered while async component is loading
    private _pendingValue: any = undefined;
    private _hasPendingValue = false;
    private _pendingOnChange: ((v: any) => void) | null = null;
    private _pendingOnTouched: ((_: any) => void) | null = null;

    constructor() {
        effect(() => {
            const type = this.descriptor().type;
            if (this.controlsFactory.isLazy(type)) {
                this.controlsFactory.resolveAsync(type).then(t => this.currentType.set(t));
            } else {
                this.currentType.set(this.controlsFactory.resolve(type));
            }
        });

        effect(() => {
            const instance = this.outlet()?.componentRef?.instance as BaseControlDirective<BaseControlDescriptor> | undefined;
            if (!instance) return;
            if (this._hasPendingValue) {
                instance.setControlValue(this._pendingValue);
                this._hasPendingValue = false;
            }
            if (this._pendingOnChange) {
                instance.registerOnValueChanged(this._pendingOnChange);
                this._pendingOnChange = null;
            }
            if (this._pendingOnTouched) {
                instance.registerOnControlTouched(this._pendingOnTouched);
                this._pendingOnTouched = null;
            }
        });
    }

    private get instance(): BaseControlDirective<BaseControlDescriptor> | null {
        return this.outlet()?.componentRef?.instance ?? null;
    }

    writeValue(obj: any): void {
        if (this.instance) {
            this.instance.setControlValue(obj);
        } else {
            this._pendingValue = obj;
            this._hasPendingValue = true;
        }
    }

    registerOnChange(fn: any): void {
        if (this.instance) {
            this.instance.registerOnValueChanged(fn);
        } else {
            this._pendingOnChange = fn;
        }
    }

    registerOnTouched(fn: any): void {
        if (this.instance) {
            this.instance.registerOnControlTouched(fn);
        } else {
            this._pendingOnTouched = fn;
        }
    }
}
```

### Почему CVA-методы остаются ручными

`ngComponentOutletInputs` не может передавать callback-функции (они не Angular-инпуты). `setControlValue`, `registerOnValueChanged`, `registerOnControlTouched` — методы экземпляра, не инпуты. Поэтому CVA-буферизация остаётся, но через `viewChild(NgComponentOutlet).componentRef.instance`.

---

## Файлы для изменения

| Файл | Изменение |
|---|---|
| [base-control.directive.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/src/app/modules/core/controls/base-control.directive.ts) | Добавить `@Input()` к `descriptor` setter, `context`, `currentForm` |
| [control-holder.component.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/src/app/modules/core/dynamics/control-holder.component.ts) | Полная замена (см. выше) |
| [control-host.directive.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/src/app/modules/core/dynamics/control-host.directive.ts) | Удалить файл |
| [dynamics/index.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/src/app/modules/core/dynamics) | Убрать экспорт `ControlHostDirective` |

Шаблон и вызывающий код (`controls-list.component.html`) **не меняются** — биндинги `[descriptor]`, `[currentForm]`, `[context]` остаются теми же.

---

## Порядок выполнения

1. `base-control.directive.ts` — добавить `@Input()` к трём свойствам
2. `control-holder.component.ts` — переписать
3. `control-host.directive.ts` — удалить
4. `dynamics/index.ts` — убрать экспорт `ControlHostDirective`
5. `npm run build` — проверить компиляцию
6. `npm test` — убедиться, что тесты зелёные

## Верификация

- Build без ошибок
- 13/13 тестов проходят
- В приложении: все контролы (datepicker, color, markdown, select, collection) отображаются и работают корректно
- Lazy-загруженные контролы (открыть секцию с тяжёлым контролом) загружаются без задержки в UI
