# Migration Report: 1.2.4-beta.8 → 2.0.3

Generated: 2026-05-01

## Automated Changes (0 files)

_No automated changes applied._

## Manual Migration Required

### shims-to-globals

- E:\Develops\VirtoWay\projects\tasks\4927\vc-module-pagebuilder\src\VirtoCommerce.PageBuilderModule.Web\Apps\page-builder-shell\tsconfig.json: already has @vc-shell/framework/globals in types

### ✅ vctable-audit

- E:\Develops\VirtoWay\projects\tasks\4927\vc-module-pagebuilder\src\VirtoCommerce.PageBuilderModule.Web\Apps\page-builder-shell\src\modules\page-builder\components\pagesList.vue: Uses <VcTable> — must be migrated to <VcDataTable>. See migration guide: VcTable → VcDataTable.

### ✅ NSwag DTO Class → Interface Migration

API client DTOs changed from classes (with `new DtoClass()`) to interfaces (with `{} as DtoClass`). The migrator handles simple cases automatically. Clone-then-mutate patterns (`new X(); x.field = value;`) require manual rewrite.

```ts
// Clone-then-mutate (manual migration):
// OLD:
const criteria = new SearchCriteria();
criteria.take = 20;
criteria.sort = "name:ASC";

// NEW:
const criteria = { take: 20, sort: "name:ASC" } as SearchCriteria;
```

> See: [migration/nswag-class-to-interface.md](migration/nswag-class-to-interface.md)

### ✅ Form Management with useBladeForm()

`useForm()` (vee-validate) + manual `onBeforeClose()` + `modified` tracking are replaced by a single `useBladeForm()` composable. Remove all three and replace with one call. `useBladeForm` handles close confirmation, modification tracking, and form validation automatically.

```ts
// OLD:
import { useForm } from "vee-validate";
const { meta } = useForm({ validateOnMount: false });
const isModified = computed(() => meta.value.dirty);
onBeforeClose(async () => {
  if (isModified.value) {
    return !(await showConfirmation(t("CLOSE_CONFIRMATION")));
  }
});

// NEW:
import { useBladeForm } from "@vc-shell/framework";
const form = useBladeForm({
  data: item, // your reactive data ref
  closeConfirmMessage: computed(() => t("CLOSE_CONFIRMATION")),
});
// form.canSave, form.isModified, form.setBaseline(), form.markReady(), form.revert()
// onBeforeClose is handled automatically — DELETE it
```

> See: [migration/37-use-blade-form.md](migration/37-use-blade-form.md)

### ✅ use-data-table-pagination-audit

- E:\Develops\VirtoWay\projects\tasks\4927\vc-module-pagebuilder\src\VirtoCommerce.PageBuilderModule.Web\Apps\page-builder-shell\src\modules\page-builder\components\pagesList.vue: Uses manual onPaginationClick — delete it and bind @pagination-click="pagination.goToPage". See migration guide: useDataTablePagination.
- E:\Develops\VirtoWay\projects\tasks\4927\vc-module-pagebuilder\src\VirtoCommerce.PageBuilderModule.Web\Apps\page-builder-shell\src\modules\page-builder\composables\usePageBuilderList\index.ts: Manual pagination triple (totalCount/pages/currentPage). Replace with useDataTablePagination(). See migration guide: useDataTablePagination.

### ✅ icon-audit

- E:\Develops\VirtoWay\projects\tasks\4927\vc-module-pagebuilder\src\VirtoCommerce.PageBuilderModule.Web\Apps\page-builder-shell\src\modules\page-builder\composables\usePagesListToolbar\index.ts: [Material] material-upload → replace with lucide- equivalent
- E:\Develops\VirtoWay\projects\tasks\4927\vc-module-pagebuilder\src\VirtoCommerce.PageBuilderModule.Web\Apps\page-builder-shell\src\modules\page-builder\pages\ActivePagesList.vue: [Material] material-article → replace with lucide- equivalent
- E:\Develops\VirtoWay\projects\tasks\4927\vc-module-pagebuilder\src\VirtoCommerce.PageBuilderModule.Web\Apps\page-builder-shell\src\modules\page-builder\pages\AllPagesList.vue: [Material] material-article → replace with lucide- equivalent
- E:\Develops\VirtoWay\projects\tasks\4927\vc-module-pagebuilder\src\VirtoCommerce.PageBuilderModule.Web\Apps\page-builder-shell\src\modules\page-builder\pages\ArchivedPagesList.vue: [Material] material-article → replace with lucide- equivalent
- E:\Develops\VirtoWay\projects\tasks\4927\vc-module-pagebuilder\src\VirtoCommerce.PageBuilderModule.Web\Apps\page-builder-shell\src\modules\page-builder\pages\DraftPagesList.vue: [Material] material-article → replace with lucide- equivalent
- E:\Develops\VirtoWay\projects\tasks\4927\vc-module-pagebuilder\src\VirtoCommerce.PageBuilderModule.Web\Apps\page-builder-shell\src\modules\page-builder\pages\PageDetails.vue: [Material] material-date_range → replace with lucide- equivalent
- E:\Develops\VirtoWay\projects\tasks\4927\vc-module-pagebuilder\src\VirtoCommerce.PageBuilderModule.Web\Apps\page-builder-shell\src\modules\page-builder\pages\PageDetails.vue: [Material] material-crop → replace with lucide- equivalent
- E:\Develops\VirtoWay\projects\tasks\4927\vc-module-pagebuilder\src\VirtoCommerce.PageBuilderModule.Web\Apps\page-builder-shell\src\modules\page-builder\pages\PageDetails.vue: [Material] material-download → replace with lucide- equivalent
- E:\Develops\VirtoWay\projects\tasks\4927\vc-module-pagebuilder\src\VirtoCommerce.PageBuilderModule.Web\Apps\page-builder-shell\src\modules\page-builder\pages\PageDetails.vue: [Material] material-description → replace with lucide- equivalent
- E:\Develops\VirtoWay\projects\tasks\4927\vc-module-pagebuilder\src\VirtoCommerce.PageBuilderModule.Web\Apps\page-builder-shell\src\modules\page-builder\pages\PageDetails.vue: [Material] material-article → replace with lucide- equivalent
- E:\Develops\VirtoWay\projects\tasks\4927\vc-module-pagebuilder\src\VirtoCommerce.PageBuilderModule.Web\Apps\page-builder-shell\src\modules\page-builder\pages\PendingPagesList.vue: [Material] material-article → replace with lucide- equivalent

### ✅ Reusable Blade Components

Components with blade props (expanded/closable) but no `defineBlade()` were skipped — they are reusable components, not blade pages. If these components pass blade props to child blades, remove the forwarding — child blades should call `useBlade()` directly.

```vue
<!-- OLD: wrapper forwarding blade props -->
<MyBlade :expanded="expanded" :closable="closable" :param="param" @close:blade="$emit('close:blade')" />

<!-- NEW: wrapper passes only domain props, child calls useBlade() -->
<MyBlade :config="config" />
```

> See: [migration/11-blade-props.md](migration/11-blade-props.md)

## Completed by AI (2026-05-01)

All "Manual Migration Required" topics above have been completed by the migration-agent. Final type-check: **0 errors**. Build: **passing**.

**nswag-class-to-interface (2 files):**
- `src/modules/page-builder/composables/usePageBuilderDetails/index.ts` — `new GroupedPageBuilderPage()` → object literal
- `src/modules/page-builder/composables/usePageBuilderList/index.ts` — `new PageBuilderPageSearchCriteria()` → object spread

**use-blade-form (2 files):**
- `src/modules/page-builder/composables/usePageBuilderDetails/index.ts` — removed `useModificationTracker`, replaced `currentValue` with direct `item` ref
- `src/modules/page-builder/pages/PageDetails.vue` — `useForm` + `onBeforeClose` → `useBladeForm`; toolbar uses `canSave`; `setBaseline()` after load/save

**blade-props-simplification (6 files):**
- `src/modules/page-builder/components/pagesList.vue` — dropped `expanded`/`closable` props, removed `blade:` wrapper from 3 `openBlade()` calls
- `pages/{Active,All,Archived,Draft,Pending}PagesList.vue` — dropped `:closable`/`:expanded` template bindings, added `param` to `useBlade()` destructure

**vctable-audit (1 file):**
- `src/modules/page-builder/components/pagesList.vue` — full rewrite from `<VcTable>` to `<VcDataTable>` + `<VcColumn>`; `useTableSort` → `useDataTableSort`; selection via `v-model:selection`; row-click signature `event: { data }`; filters via `:global-filters` + `@filter`; added `state-key="page_builder_pages_list"`

**use-data-table-pagination-audit (2 files):**
- `src/modules/page-builder/composables/usePageBuilderList/index.ts` — added `useDataTablePagination()`; replaced totalCount/pages/currentPage triple with single `pagination`
- `src/modules/page-builder/components/pagesList.vue` — removed manual `onPaginationClick`; binds `@pagination-click="pagination.goToPage"`

**icon-audit (7 files):**
All `material-*` icons → lucide equivalents (`material-article` → `lucide-file-text`/`lucide-archive`, `material-upload` → `lucide-upload`, `material-date_range` → `lucide-calendar-range`, `material-crop` → `lucide-crop`, `material-download` → `lucide-download`, `material-description` → `lucide-send`).

## Post-Migration Fixes (not flagged by migrator)

API regeneration with Interface style relocated three methods from `PageBuilderPageClient` to `PageBuilderPageSettingsClient`. Updated consumers:
- `src/modules/page-builder/composables/useOrganizations/index.ts` — switched client to `PageBuilderPageSettingsClient`
- `src/modules/page-builder/composables/useUserGroups/index.ts` — switched client to `PageBuilderPageSettingsClient`

Also fixed legacy TypeScript prefix-cast that broke the migrator parser:
- `src/modules/page-builder/composables/usePagesListToolbar/index.ts` — `<string[]>(<unknown>x)` → `(x as unknown as string[])`

## Not Covered by Migrator

_These migration guides may be relevant — check manually:_

- **29-vc-table-to-data-table** — Old VcTable → VcDataTable migration
  Check: `grep -rn "VcTable\b" src/`

<details>
<summary>Transform Log (1 entries)</summary>

- Registry: 0 DTO classes, 0 interface→class mappings, package: api

</details>
