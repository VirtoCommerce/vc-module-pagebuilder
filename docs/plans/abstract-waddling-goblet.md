# Plan: Angular 21 Upgrade — page-builder-designer

## Context

Текущая версия Angular в `page-builder-designer` — **20.3.x**. Angular 21 вышел в ноябре 2025 года. Помимо механического поднятия версий пакетов, хочется пройтись по контролам приложения и внутренним библиотекам (`ngv-markdown`, `ngv-datepicker`) и устранить устаревшие паттерны, которые появились или получили альтернативы в Angular 21.

Кодовая база уже в хорошем состоянии: standalone-компоненты, `input()`/`output()`/`viewChild()`, `@if`/`@for`, `inject()`, `takeUntilDestroyed` — всё это есть. Апгрейд будет эволюционным, без переписывания.

---

## 1. Обновление зависимостей

### 1.1 Обязательные обновления

| Пакет | Текущая версия | Целевая версия | Примечание |
|---|---|---|---|
| `@angular/core` и все `@angular/*` | ^20.3.18 | ^21.x | Единый мажор |
| `@angular/cdk`, `@angular/material` | ^20.2.14 | ^21.x | В паре с core |
| `@angular/cli`, `@angular-devkit/*`, `@angular/compiler-cli` | ^20.3.x | ^21.x | |
| `@ngrx/store`, `@ngrx/effects`, `@ngrx/router-store`, `@ngrx/store-devtools` | ^20.1.0 | ^21.x | NgRx v21 для Angular v21 |
| `ng-packagr` | ^20.3.2 | ^21.x | Сборщик внутренних либ |
| `typescript` | ~5.8.3 | ~5.9.x | Angular 21 требует TS ≥5.9 |
| `@ng-select/ng-select` | ^20.7.0 | ^21.x | Версия выровнена с Angular |
| `ngx-cookie-service` | ^20.1.1 | ^21.x | Версия выровнена с Angular |

### 1.2 Зависимости, которые не следуют Angular-версионированию

| Пакет | Текущая версия | Действие |
|---|---|---|
| `ngx-scrollbar` | ^19.1.4 | Оставить v19 — совместимо с Angular 21 |
| `ngx-toastr` | ^19.1.0 | Поднять до ^20.x — есть OnPush + Signals улучшения |
| `ngx-color` | ^10.1.0 | Проверить совместимость (v21.0.0 вышел для angular/ngx-color-picker) |
| `ngx-tiptap` | ^10.0.0 | Проверить совместимость с Angular 21 peer deps |
| `ckeditor4-angular` | ^5.2.1 | Остаётся — проверить только совместимость peer deps |

### 1.3 peer dependencies в ngv-markdown и ngv-datepicker

Обновить в обоих `projects/*/package.json` (поле `peerDependencies`):
- `@angular/core`: `^20.x` → `^21.x`
- `@angular/material`: `^20.x` → `^21.x` (ngv-datepicker)
- `@angular/cdk`: `^20.x` → `^21.x` (ngv-datepicker)

**Файлы:**
- [projects/ngv-markdown/package.json](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/projects/ngv-markdown/package.json)
- [projects/ngv-datepicker/package.json](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/projects/ngv-datepicker/package.json)

---

## 2. Удаление устаревших API

### 2.1 `enableProdMode()` — убрать из main.ts

В Angular 20+ `enableProdMode()` задепрекейчен — production-режим определяется автоматически по build-конфигурации.

**Файл:** [src/main.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/src/main.ts)

```typescript
// Удалить:
import { enableProdMode } from '@angular/core';
if (environment.production) {
    enableProdMode();
}
```

### 2.2 `@HostBinding` / `@HostListener` → `host` metadata

`@HostBinding` и `@HostListener` не deprecated, но Angular-команда рекомендует переносить их в `host: {}` внутри `@Component`/`@Directive` — это более явно и не требует отдельных декораторов.

**Найдены в 6 компонентах:**

| Файл | Текущий паттерн | Что заменить |
|---|---|---|
| `icon.component.ts` | `@HostBinding('class.inline')` / `'class.hoverable'` / `'class.small-size'` | `host: { '[class.inline]': 'inline()', ... }` |
| `sidebar.component.ts` | `@HostBinding('class.hidden')` / `'class.desktop-50'` | `host: { '[class.hidden]': '...', ... }` |
| `preview-area.component.ts` | `@HostBinding('class.desktop-50')` | `host: { '[class.desktop-50]': '...' }` |
| `drag-handle.component.ts` | `@HostBinding('class.visible')` | `host: { '[class.visible]': 'visible()' }` |
| `overlap-panel.component.ts` | `@HostBinding('class.inplace')` + `@HostListener('window:resize')` | `host: { '[class.inplace]': '...', '(window:resize)': 'onResize()' }` |
| `app.component.ts` | `@HostListener('window:keyup', ['$event'])` | `host: { '(window:keyup)': 'onKeyUp($event)' }` |

---

## 3. Модернизация контролов (app/modules/core/controls/)

Контролы уже хорошо написаны. Точечные улучшения, ставшие доступными/рекомендованными в Angular 21:

### 3.1 `ChangeDetectorRef` → `markForCheck()` / сигналы

В `color.component.ts` и `search.component.ts` `ChangeDetectorRef` используется для принудительного обновления после async-событий. Проверить, можно ли заменить на сигнальный стейт — тогда `cdr.detectChanges()` не нужен.

- [color.component.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/src/app/modules/core/controls/color.component.ts) — `cdr.detectChanges()` в `applyNewValue()`
- [search.component.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/src/app/modules/core/controls/search.component.ts) — `cdk.detectChanges()` после загрузки данных

### 3.2 Убрать закомментированный legacy-код

В `logo.component.html` — закомментированный `*ngIf`. Удалить вместе с мёртвым кодом.

---

## 4. Модернизация ngv-datepicker

`ngv-datepicker` — самая legacy-часть проекта. В ней:
- NgModule-based providers (`NativeDateModule`, `MatNativeDateModule`, `DateFnsModule`, `MatDateFnsModule`)
- Constructor injection в части компонентов (`MatCalendarHeader`)
- `@Input`/`@Output` декораторы (не signal-based) в `datepicker-input.ts`
- `@HostBinding` в `calendar.ts`

**Что делать в рамках этой задачи:**

### 4.1 Перевести NgModule-провайдеры на standalone

Заменить `@NgModule` в `core/datetime/index.ts` и `date-fns/index.ts` на standalone-провайдеры:

```typescript
// Вместо:
@NgModule({ providers: [{ provide: DateAdapter, useClass: NativeDateAdapter }] })
export class NativeDateModule {}

// Станет:
export const NATIVE_DATE_PROVIDERS: Provider[] = [
    { provide: DateAdapter, useClass: NativeDateAdapter },
    ...
];
// + функция provide*() по аналогии с Angular Material
export function provideNativeDateAdapter(): Provider[] { return NATIVE_DATE_PROVIDERS; }
```

**Файлы:**
- [projects/ngv-datepicker/src/lib/core/datetime/index.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/projects/ngv-datepicker/src/lib/core/datetime/index.ts)
- [projects/ngv-datepicker/src/lib/date-fns/index.ts](src/VirtoCommerce.PageBuilderModule.Web/Apps/page-builder-designer/projects/ngv-datepicker/src/lib/date-fns/index.ts)

Обновить `public-api.ts` в ngv-datepicker — экспортировать `provide*()` функции вместо NgModule-классов.

### 4.2 Constructor injection → inject() в ключевых компонентах

В `calendar.ts` (MatCalendarHeader) — заменить конструктор на `inject()`.

### 4.3 @HostBinding → host metadata в calendar.ts

По аналогии с п. 2.2 — перенести в `host: {}`.

### 4.4 `@Input`/`@Output` в datepicker-input.ts

Оставить getter/setter `@Input` там, где есть side effects (это оправдано по CLAUDE.md). Простые `@Input` без логики — можно конвертировать в `input()`.

---

## 5. ngv-markdown — минимальные изменения

Компонент уже написан современно (`input()`, `output()`, `afterNextRender()`, `DestroyRef`). Только обновить peer deps.

---

## 6. Опциональные улучшения (вне этого PR)

Следующие вещи — отдельные задачи, не блокируют Angular 21:

- **Vitest вместо Karma** — Angular 21 использует Vitest по умолчанию для новых проектов; миграция существующего — отдельная работа
- **Zoneless** — Angular 21 создаёт новые проекты без zone.js; для текущего проекта — отдельная большая задача
- **Signal Forms** — новый API форм в Angular 21; текущие `ReactiveFormsModule`-формы продолжают работать

---

## Порядок выполнения

1. Обновить `package.json` — поднять все Angular-пакеты до v21, TypeScript до 5.9, NgRx до v21, ng-packagr до v21, ng-select до v21, ngx-cookie-service до v21
2. Запустить `npm install`, исправить конфликты зависимостей
3. Обновить peer deps в `ngv-markdown/package.json` и `ngv-datepicker/package.json`
4. Удалить `enableProdMode()` из `main.ts`
5. Заменить `@HostBinding`/`@HostListener` на `host: {}` в 6 компонентах
6. Провести модернизацию ngv-datepicker: NgModule → provide functions, constructor injection → inject()
7. Проверить `ChangeDetectorRef` в controls — убрать там, где можно
8. Удалить закомментированный мёртвый код
9. Запустить `npm run build` — исправить ошибки компиляции
10. Запустить `npm test` — убедиться, что тесты зелёные
11. Запустить `npm run lint`

## Верификация

- `npm run build` завершается без ошибок и предупреждений
- `npm test` — все тесты проходят
- `npm run lint` — нет ошибок
- В браузере: приложение загружается, controls работают (datepicker, color, markdown, select)
- В `package.json` нет `^20.x` версий Angular-пакетов
