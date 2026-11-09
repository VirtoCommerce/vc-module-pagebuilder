<template>
  <div class="pd-entity">
    <span class="pd-entity__label">{{ $t("PAGE_BUILDER.DESIGNER.TEMPLATES.PREVIEW_WITH") }}</span>
    <VcSelect
      :key="type"
      class="pd-entity__select"
      size="small"
      :model-value="modelValue"
      :options="options"
      option-value="id"
      option-label="name"
      :placeholder="$t(PLACEHOLDERS[type])"
      searchable
      emit-value
      @update:model-value="pick"
    />
  </div>
</template>

<script setup lang="ts">
import { VcSelect } from "@vc-shell/framework/ui";
import { searchCategories, searchPages, searchProducts, type CatalogContext, type NamedEntity } from "../api";
import type { PageType } from "../templates";

const props = defineProps<{
  type: PageType;
  catalog: CatalogContext;
  modelValue?: string;
}>();
const emit = defineEmits<{ "update:modelValue": [value: string]; picked: [entity: NamedEntity] }>();

const PLACEHOLDERS: Record<PageType, string> = {
  product: "PAGE_BUILDER.DESIGNER.TEMPLATES.PICK_PRODUCT",
  category: "PAGE_BUILDER.DESIGNER.TEMPLATES.PICK_CATEGORY",
  page: "PAGE_BUILDER.DESIGNER.TEMPLATES.PICK_PAGE",
};

let shown: NamedEntity[] = [];
async function find(keyword?: string, ids?: string[]): Promise<NamedEntity[]> {
  if (props.type === "product") return searchProducts(props.catalog, keyword, ids);
  if (props.type === "category") return searchCategories(props.catalog, keyword, ids);
  const pages = (await searchPages(props.catalog.storeId, keyword)).map((x) => ({ id: x.permalink, name: `${x.name} (${x.permalink})` }));
  return ids?.length ? [...pages.filter((x) => ids.includes(x.id)), ...ids.filter((id) => !pages.some((x) => x.id === id)).map((id) => ({ id, name: id }))] : pages;
}
async function options(keyword?: string, _skip?: number, ids?: string[]) {
  const results = await find(keyword, ids).catch(() => []);
  shown = [...shown, ...results];
  return { results, totalCount: results.length };
}
function pick(id: unknown) {
  if (typeof id !== "string" || !id) return;
  emit("update:modelValue", id);
  const entity = shown.find((x) => x.id === id);
  if (entity) emit("picked", entity);
}
</script>

<style lang="scss" scoped>
.pd-entity {
  flex: none;
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
  font-size: 13px;

  &__label {
    flex: none;
    color: var(--neutrals-500);
    white-space: nowrap;
  }

  &__select {
    width: 260px;
    min-width: 0;

    // A long name is cut so that it does not cover the bar's next item.
    :deep(.vc-select__control) {
      overflow: hidden;
    }

    :deep(.vc-select__field-wrapper) {
      flex: 1;
      min-width: 0;
    }
  }
}
</style>
