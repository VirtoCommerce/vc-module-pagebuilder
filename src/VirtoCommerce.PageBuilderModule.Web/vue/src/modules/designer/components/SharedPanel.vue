<template>
  <div class="pd-shared">
    <p class="pd-shared__hint">{{ $t("PAGE_BUILDER.DESIGNER.SHARED_PANEL.HINT") }}</p>
    <p
      v-if="!canInsert"
      class="pd-shared__hint"
    >
      {{ $t("PAGE_BUILDER.DESIGNER.SHARED_ON_PAGES_ONLY") }}
    </p>
    <VcInput
      v-model="search"
      size="small"
      clearable
      :placeholder="$t('PAGE_BUILDER.DESIGNER.SHARED_PANEL.SEARCH')"
    />
    <ul
      v-if="components.length"
      class="pd-shared__list"
    >
      <li
        v-for="component in components"
        :key="component.id"
        class="pd-shared__item"
      >
        <VcIcon
          icon="lucide-link"
          size="s"
          class="pd-shared__icon"
        />
        <span class="pd-shared__name">
          {{ component.name }}
          <small>{{ $t("PAGE_BUILDER.DESIGNER.USED_ON", { count: component.usageCount ?? 0 }) }}</small>
        </span>
        <VcButton
          v-if="canInsert"
          variant="ghost"
          size="xs"
          icon="lucide-plus"
          :title="$t('PAGE_BUILDER.DESIGNER.SHARED_PANEL.INSERT')"
          :aria-label="$t('PAGE_BUILDER.DESIGNER.SHARED_PANEL.INSERT')"
          @click="emit('insert', component)"
        />
        <VcButton
          variant="ghost"
          size="xs"
          icon="lucide-pencil"
          :title="$t('PAGE_BUILDER.DESIGNER.SHARED_PANEL.EDIT')"
          :aria-label="$t('PAGE_BUILDER.DESIGNER.SHARED_PANEL.EDIT')"
          @click="emit('edit', component)"
        />
      </li>
    </ul>
    <p
      v-else-if="!loading"
      class="pd-shared__empty"
    >
      {{ $t(search ? "PAGE_BUILDER.DESIGNER.NO_SECTIONS" : "PAGE_BUILDER.DESIGNER.SHARED_PANEL.EMPTY") }}
    </p>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from "vue";
import { debounce } from "lodash-es";
import { VcButton, VcIcon, VcInput } from "@vc-shell/framework/ui";
import { searchSharedComponents, type SharedComponent } from "../api";

const props = defineProps<{
  storeId?: string;
  canInsert?: boolean;
}>();
const emit = defineEmits<{ insert: [component: SharedComponent]; edit: [component: SharedComponent] }>();

const search = ref<string>();
const components = ref<SharedComponent[]>([]);
const loading = ref(true);
let searches = 0;
const load = debounce(async () => {
  const request = ++searches;
  const found = props.storeId ? await searchSharedComponents(props.storeId, search.value).catch(() => []) : [];
  // An earlier search that answers last must not replace a later one's results.
  if (request !== searches) return;
  components.value = found;
  loading.value = false;
}, 250);
watch([search, () => props.storeId], load, { immediate: true });

defineExpose({ reload: load });
</script>

<style lang="scss" scoped>
.pd-shared {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 12px 8px;

  &__hint,
  &__empty {
    margin: 0;
    color: var(--neutrals-500);
    font-size: 12px;
    line-height: 16px;
  }

  &__empty {
    padding: 8px 4px;
  }

  &__list {
    margin: 0;
    padding: 0;
    list-style: none;
  }

  &__item {
    display: flex;
    align-items: center;
    gap: 4px;
    padding: 6px 4px 6px 8px;
    border-radius: 6px;

    &:hover {
      background: var(--neutrals-100);
    }
  }

  &__icon {
    flex: none;
    color: var(--neutrals-500);
  }

  &__name {
    flex: 1;
    min-width: 0;
    display: flex;
    flex-direction: column;
    overflow: hidden;
    color: var(--neutrals-900);
    font-size: 13px;
    text-overflow: ellipsis;
    white-space: nowrap;

    small {
      color: var(--neutrals-500);
      font-size: 11px;
    }
  }
}
</style>
