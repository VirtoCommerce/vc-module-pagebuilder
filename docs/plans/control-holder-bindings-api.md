# Рефакторинг: ControlHolderComponent → createComponent с bindings API

## Контекст

Angular 20 добавил опцию `bindings` в `ViewContainerRef.createComponent()`, которая позволяет декларативно передавать inputs и подписываться на outputs без ручных `setInput()`, `effect()` и `.subscribe()`:

```typescript
vcr.createComponent(MyComponent, {
    bindings: [
        inputBinding('someInput', mySignal),        // реактивно обновляется при изменении сигнала
        outputBinding('someOutput', (v) => fn(v)),   // callback вместо .subscribe()
    ]
});
```

**Текущее состояние `control-holder.component.ts`** после предыдущего рефакторинга:
- `ControlHostDirective` + `viewChild` — нужен только для получения `ViewContainerRef`
- 3 отдельных `effect()` — вручную синхронизируют `currentForm`, `context`, `onControlTouched`
- В `createComponent`: ручные `ref.setInput(...)` × 5 + `ref.instance.valueChanged.subscribe(...)`
- Хранит `_componentRef` сигнал исключительно для координации этих эффектов

Всё это заменяется одним вызовом `createComponent` с `bindings`.

---

## Что меняется

### 1. `control-holder.component.ts` — полная замена

**Убрать:**
- `ControlHostDirective`, `viewChild`, `ControlHostDirective` из `imports`
- `_componentRef` сигнал
- все три `effect()`
- ручные `ref.setInput(...)` в `createComponent`
- `ref.instance.valueChanged.subscribe(...)`
- `ComponentRef` из импортов

**Добавить:**
- `inject(ViewContainerRef)` напрямую
- `inputBinding`, `outputBinding` из `@angular/core`
- `_onTouched = signal<(_: any) => void>(() => {})` — убрать `| null`, всегда функция

**Результат:**

```typescript
@Component({
    selector: 'app-control-holder',
    template: ``,
    providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => ControlHolderComponent), multi: true }],
    changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ControlHolderComponent implements ControlValueAccessor {
    private readonly vcr = inject(ViewContainerRef);
    private readonly controlsFactory = inject(ControlsFactory);

    readonly descriptor = input.required<BaseControlDescriptor>();
    readonly currentForm = input.required<UntypedFormGroup>();
    readonly context = input.required<ControlContext>();

    private readonly _controlValue = signal<any>(null);
    private _onChange: ((v: any) => void) | null = null;
    private readonly _onTouched = signal<(_: any) => void>(() => {});

    constructor() {
        afterNextRender(() => {
            const type = this.descriptor().type;
            const create = (t: Type<any>) => this.vcr.createComponent(t, {
                bindings: [
                    inputBinding('descriptor', this.descriptor),
                    inputBinding('currentForm', this.currentForm),
                    inputBinding('context', this.context),
                    inputBinding('controlValue', this._controlValue),
                    inputBinding('onControlTouched', this._onTouched),
                    outputBinding('valueChanged', (v: any) => this._onChange?.(v)),
                ]
            });
            this.controlsFactory.isLazy(type)
                ? this.controlsFactory.resolveAsync(type).then(create)
                : create(this.controlsFactory.resolve(type));
        });
    }

    writeValue(obj: any): void {
        this._controlValue.set((!obj && obj !== 0 && obj !== BigInt(0)) ? null : obj);
    }
    registerOnChange(fn: any): void { this._onChange = fn; }
    registerOnTouched(fn: any): void { this._onTouched.set(fn); }
}
```

Почему это работает:
- `inputBinding('descriptor', this.descriptor)` — Angular реактивно вызывает `setInput('descriptor', v)` каждый раз, когда сигнал меняется. Работает как с `@Input()` setter, так и с `input()` сигналами на дочернем компоненте.
- `inputBinding('controlValue', this._controlValue)` — то же самое для CVA значения
- `outputBinding('valueChanged', fn)` — Angular подписывается на `output()` декларацию и вызывает callback при каждом emit

---

### 2. `control-host.directive.ts` — удалить файл

Был нужен исключительно для получения `ViewContainerRef` через `<ng-template>`. Теперь `ViewContainerRef` инжектируется напрямую.

---

### 3. `dynamics/index.ts` — убрать `ControlHostDirective`

```typescript
// Убрать:
import { ControlHostDirective } from './control-host.directive';
// и из DYNAMIC_COMPONENTS
```

---

### 4. `control-holder.component.scss` — проверить

Убедиться что стили не опираются на DOM-структуру с `<ng-template>`. Скорее всего ничего менять не нужно.

---

## Что НЕ меняется

- `base-control.directive.ts` — уже обновлён, совместим с `inputBinding`/`outputBinding`
- Подклассы (`object`, `collection`, `markdown`, `search`, `text`) — уже обновлены
- `controls-list.component.html` — биндинги на `app-control-holder` остаются теми же
- `ControlsFactory` — не меняется

---

## Замечания по DOM

`inject(ViewContainerRef)` в компоненте возвращает view container хост-элемента компонента. Динамически созданный компонент будет рендериться внутри `<app-control-holder>` — то же место, что и раньше при использовании `<ng-template appControlHost />`. Визуально разницы нет.

---

## Порядок выполнения

1. Переписать `control-holder.component.ts`
2. Удалить `control-host.directive.ts`
3. Обновить `dynamics/index.ts`
4. `npm run build` — проверить компиляцию
5. `npm test` — запустить тесты

## Верификация

- Build без ошибок
- Тесты проходят
- Контролы отображаются, значения читаются и записываются корректно
- Lazy-загрузка (CKEditor, markdown, calendar, color) работает
