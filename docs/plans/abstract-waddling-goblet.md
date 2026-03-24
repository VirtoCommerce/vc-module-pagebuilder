# Refactoring: ControlHolderComponent → чистый NgComponentOutlet

## Context

После предыдущего рефакторинга `ControlHolderComponent` использует `ViewContainerRef.createComponent()` и хранит экземпляр в `signal`. Работает хорошо, но всё ещё нуждается в прямом вызове методов на экземпляре (`setControlValue`, `registerOnValueChanged`, `registerOnControlTouched`) — то есть в знании внутреннего интерфейса компонента.

Цель: полностью декларативный `ControlHolderComponent` — он передаёт данные только через inputs/outputs, не знает о `BaseControlDirective` вообще. Удаляем `ControlHostDirective` и прямой вызов методов.

**Почему возможно:** `NgComponentOutlet` с `ngComponentOutletInputs` + `ngComponentOutletOutputs` (Angular 17+) покрывает всю коммуникацию. `_componentRef` не нужен вообще.

---

## Архитектура после рефакторинга

```
ControlHolderComponent
  ├── ngComponentOutletInputs: { descriptor, currentForm, context, controlValue, onControlTouched }
  └── ngComponentOutletOutputs: { valueChanged: fn }
          ↕
  BaseControlDirective
    ├── @Input() descriptor (геттер/сеттер, вызывает descriptorChanged())
    ├── @Input() currentForm
    ├── @Input() context
    ├── input() controlValue  ← уже есть
    ├── @Input() onControlTouched  ← новый
    ├── output() valueChanged  ← уже есть, emitвсегда через defaultValueChanged
    └── effect() → applyNewValue() при каждом изменении _controlValueInput
```

---

## Изменения по файлам

### 1. `base-control.directive.ts`

**Добавить:**
- `@Input() onControlTouched = (_: any) => {};` — было обычным полем, делаем Angular-инпутом
- `effect()` в конструкторе, вызывающий `applyNewValue()` при изменении `_controlValueInput`:
  ```typescript
  constructor() {
      effect(() => {
          this._controlValueInput(); // отслеживаем
          untracked(() => this.applyNewValue());
      });
  }
  ```

**Убрать:**
- `registerOnValueChanged(fn)` — больше не нужен (связь через `ngComponentOutletOutputs`)
- `registerOnControlTouched(fn)` — больше не нужен (связь через input)

**Оставить:**
- `setControlValue` — используется внутри `onAction` и в переопределениях подклассов, остаётся как protected/internal метод

**Файл:** [base-control.directive.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/src/app/modules/core/controls/base-control.directive.ts)

---

### 2. Подклассы — переход с `setControlValue` на `applyNewValue`

Три компонента переопределяют `setControlValue` для синхронизации внутреннего состояния при внешнем изменении значения. Переносим логику в `applyNewValue()`.

#### `markdown.component.ts`

```typescript
// Было:
override setControlValue(value: any): void {
    // ... normalize value ...
    this.controlValue.set(result);  // не вызывает super!
}

// Стало:
override applyNewValue(): void {
    const value = this._controlValueInput();  // читаем raw input
    // ... та же нормализация ...
    this.controlValue.set(result);
}
```

#### `search.component.ts`

```typescript
// Было:
override setControlValue(value: any) {
    if (!value) { value = { __nodata: true, __searchQuery: null }; }
    super.setControlValue(value);
}

// Стало:
override applyNewValue(): void {
    if (!this.controlValue()) {
        this.controlValue.set({ __nodata: true, __searchQuery: null });
    }
}
```

#### `object.component.ts`

```typescript
// Было: override setControlValue + override registerOnValueChanged

// Стано:
override applyNewValue(): void {
    const v = this.controlValue() || {};
    const descriptors = this.objectDescriptors();
    this.objectForm = formsHelpers.generateForm(v, descriptors);
    this.formReset$.next();
    this.objectForm.valueChanges.pipe(
        takeUntil(this.formReset$),
        takeUntilDestroyed(this.destroyRef)
    ).subscribe(x => {
        this.defaultValueChanged(x);  // вместо this.onValueChanged(x)
    });
}
// Удалить: override registerOnValueChanged
```

**Почему `defaultValueChanged` вместо `onValueChanged`:** `defaultValueChanged` делает `controlValue.set(x)` + `valueChanged.emit(x)`. Emit нужен, чтобы `ngComponentOutletOutputs` передал значение в CVA.

**Файлы:**
- [markdown.component.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/src/app/modules/core/controls/markdown/markdown.component.ts)
- [search.component.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/src/app/modules/core/controls/search/search.component.ts)
- [object.component.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/src/app/modules/core/controls/object/object.component.ts)

---

### 3. `text.component.ts` — переопределение `registerOnValueChanged` → `onValueChanged`

CKEditor может эмитировать событие при программной установке значения. Текущий override добавляет проверку дедупликации.

```typescript
// Было:
override registerOnValueChanged(fn: (_: any) => void) {
    this.onValueChanged = (newValue) => {
        if (this.controlValue() !== newValue) {
            fn(newValue);  // не вызывает defaultValueChanged!
        }
    }
}

// Стало (override свойства напрямую, без registerOnValueChanged):
override onValueChanged = (newValue: any) => {
    if (this.controlValue() !== newValue) {
        this.defaultValueChanged(newValue);  // sets signal + emits valueChanged
    }
};
// Удалить: override registerOnValueChanged
```

**Почему работает:** `defaultValueChanged` устанавливает `controlValue.set(newValue)`. При следующем вызове от CKEditor проверка `controlValue() !== newValue` вернёт `false` (значения равны) → цикл прерывается.

**Файл:** [text.component.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/src/app/modules/core/controls/text/text.component.ts)

---

### 4. `control-holder.component.ts` — полная замена на NgComponentOutlet

```typescript
@Component({
    selector: 'app-control-holder',
    template: `
        @if (currentType(); as type) {
            <ng-container
                [ngComponentOutlet]="type"
                [ngComponentOutletInputs]="componentInputs()"
                [ngComponentOutletOutputs]="componentOutputs">
            </ng-container>
        }
    `,
    providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(...), multi: true }],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgComponentOutlet]  // из @angular/common
})
export class ControlHolderComponent implements ControlValueAccessor {
    private readonly controlsFactory = inject(ControlsFactory);

    readonly descriptor = input.required<BaseControlDescriptor>();
    readonly currentForm = input.required<UntypedFormGroup>();
    readonly context = input.required<ControlContext>();

    readonly currentType = signal<Type<any> | null>(null);

    private readonly _controlValue = signal<any>(null);
    private _onChange: ((v: any) => void) | null = null;
    private readonly _onTouched = signal<((_: any) => void) | null>(null);

    readonly componentInputs = computed(() => ({
        descriptor: this.descriptor(),
        currentForm: this.currentForm(),
        context: this.context(),
        controlValue: this._controlValue(),
        onControlTouched: this._onTouched() ?? (() => {}),
    }));

    // Объявляем один раз — читает _onChange при каждом вызове
    readonly componentOutputs = {
        valueChanged: (v: any) => this._onChange?.(v),
    };

    constructor() {
        effect(() => {
            const type = this.descriptor().type;
            if (this.controlsFactory.isLazy(type)) {
                this.controlsFactory.resolveAsync(type).then(t => this.currentType.set(t));
            } else {
                this.currentType.set(this.controlsFactory.resolve(type));
            }
        });
    }

    writeValue(obj: any): void {
        const normalized = (!obj && obj !== 0 && obj !== BigInt(0)) ? null : obj;
        this._controlValue.set(normalized);
    }

    registerOnChange(fn: any): void { this._onChange = fn; }
    registerOnTouched(fn: any): void { this._onTouched.set(fn); }
}
```

**Почему нет буферизации pending state:** Состояние хранится в сигналах (`_controlValue`, `_onTouched`). Когда `NgComponentOutlet` создаёт компонент (даже после задержки при lazy load), `componentInputs()` сразу передаёт актуальные значения. `componentOutputs.valueChanged` читает `_onChange` в момент вызова — всегда актуально.

**Файл:** [control-holder.component.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/src/app/modules/core/dynamics/control-holder.component.ts)

---

### 5. `control-host.directive.ts` — удалить файл

Больше не используется. Убрать из `dynamics/index.ts`.

**Файлы:**
- [control-host.directive.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/src/app/modules/core/dynamics/control-host.directive.ts) — удалить
- [dynamics/index.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/src/app/modules/core/dynamics) — убрать экспорт

---

## Что НЕ меняется

- `controls-list.component.html` — биндинги `[descriptor]`, `[currentForm]`, `[context]`, `[formControlName]` остаются теми же
- `onAction` в `BaseControlDirective` — вызывает `setControlValue` внутренне, поведение прежнее
- `onValueChanged` в остальных подклассах (checkbox, color, string, calendar, number, files, select) — вызывают `this.onValueChanged(value)` → `defaultValueChanged` → `valueChanged.emit` — работает через `ngComponentOutletOutputs`

---

## Порядок выполнения

1. `base-control.directive.ts` — добавить `@Input() onControlTouched`, конструктор с `effect`, удалить `registerOnValueChanged`/`registerOnControlTouched`
2. `text.component.ts` — заменить `registerOnValueChanged` на `override onValueChanged`
3. `object.component.ts` — `setControlValue` + `registerOnValueChanged` → `applyNewValue`
4. `markdown.component.ts` — `setControlValue` → `applyNewValue`
5. `search.component.ts` — `setControlValue` → `applyNewValue`
6. `control-holder.component.ts` — полная замена
7. Удалить `control-host.directive.ts`, обновить `index.ts`
8. `npm run build` — проверить компиляцию
9. `npm test` — проверить тесты

## Верификация

- Build без ошибок
- 13/13 тестов проходят
- В приложении: контролы отображаются, значения читаются и сохраняются корректно
- Особо проверить: `ObjectComponent` (дочерняя форма), `TextComponent` (CKEditor dedup), `MarkdownComponent` (трансформация значения), `SearchComponent` (нормализация null), lazy-загрузка тяжёлых контролов
