<template>
  <div class="pd-add">
    <div class="pd-add__head">
      <VcButton
        variant="ghost"
        size="icon-sm"
        icon="lucide-arrow-left"
        :aria-label="$t('PAGE_BUILDER.DESIGNER.BACK')"
        @click="close"
      />
      <span class="pd-add__title">{{ $t("PAGE_BUILDER.DESIGNER.ADD_SECTION") }}</span>
    </div>
    <VcInput
      v-model="search"
      size="small"
      clearable
      autofocus
      :placeholder="$t('PAGE_BUILDER.DESIGNER.SEARCH_SECTIONS')"
    />
    <template v-if="shared.length && !designer.shared.value && !group">
      <div class="pd-add__group">{{ $t("PAGE_BUILDER.DESIGNER.SHARED_COMPONENTS") }}</div>
      <ul class="pd-add__list">
        <li
          v-for="component in shared"
          :key="component.id"
        >
          <button
            type="button"
            class="pd-add__item"
            @click="addShared(component)"
          >
            <VcIcon
              icon="lucide-link"
              size="m"
              class="pd-add__icon"
            />
            <span class="pd-add__name">{{ component.name }}</span>
            <span class="pd-add__meta">{{ $t("PAGE_BUILDER.DESIGNER.USED_ON", { count: component.usageCount ?? 0 }) }}</span>
          </button>
        </li>
      </ul>
      <div class="pd-add__group">{{ $t("PAGE_BUILDER.DESIGNER.SECTIONS") }}</div>
    </template>
    <ul class="pd-add__list">
      <li
        v-for="{ type, schema } in shown"
        :key="type"
      >
        <button
          type="button"
          class="pd-add__item"
          @click="add(type)"
          @mouseenter="!group && emit('preview', type)"
          @mouseleave="!group && emit('preview', null)"
          @focus="!group && emit('preview', type)"
          @blur="!group && emit('preview', null)"
        >
          <VcIcon
            :icon="iconOf(schema)"
            size="m"
            class="pd-add__icon"
          />
          {{ schema.name ?? type }}
        </button>
      </li>
      <li
        v-if="!shown.length"
        class="pd-add__empty"
      >
        {{ $t("PAGE_BUILDER.DESIGNER.NO_SECTIONS") }}
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
// The section types the store's theme offers; hovering one shows it at the end of the preview, a click adds it.
// For a section group (the header, the footer): the types that group takes.
import { computed, ref, watch } from "vue";
import { debounce } from "lodash-es";
import { VcButton, VcIcon, VcInput } from "@vc-shell/framework/ui";
import type { Designer } from "../useDesigner";
import type { SectionGroup } from "../types";
import { iconOf } from "../schema";
import { searchSharedComponents, type SharedComponent } from "../api";

const props = defineProps<{ designer: Designer; group?: SectionGroup }>();
const emit = defineEmits<{ preview: [type: string | null]; close: [] }>();

const search = ref<string>();
const shown = computed(() => {
  const keyword = search.value?.trim().toLowerCase();
  const types = props.group ? props.designer.groupSectionTypes(props.group) : props.designer.sectionTypes.value;
  return types.filter((x) => !keyword || (x.schema.name ?? x.type).toLowerCase().includes(keyword));
});

// the store's shared components, searched with the same box (none listed when the user can't read them)
const shared = ref<SharedComponent[]>([]);
const loadShared = debounce(async () => {
  shared.value = props.designer.storeId.value ? await searchSharedComponents(props.designer.storeId.value, search.value).catch(() => []) : [];
}, 250);
watch(search, loadShared, { immediate: true });

function addShared(component: SharedComponent) {
  props.designer.insertShared(component);
  close();
}

function add(type: string) {
  props.designer.addSection(type, props.group);
  close();
}
function close() {
  emit("preview", null);
  emit("close");
}
</script>

<style lang="scss" scoped>
.pd-add {
  display: flex;
  flex-direction: column;
  gap: 8px;

  &__head {
    display: flex;
    align-items: center;
    gap: 6px;
  }

  &__title {
    font-weight: 600;
    color: var(--neutrals-900);
  }

  &__list {
    margin: 0;
    padding: 0;
    list-style: none;
  }

  &__item {
    width: 100%;
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 8px;
    border: 0;
    border-radius: 6px;
    background: none;
    color: var(--neutrals-900);
    font: inherit;
    text-align: left;
    cursor: pointer;

    &:hover,
    &:focus-visible {
      background: var(--neutrals-100);
      outline: none;
    }
  }

  &__icon {
    color: var(--neutrals-600);
  }

  &__group {
    padding: 10px 8px 4px;
    font-size: 12px;
    font-weight: 600;
    color: var(--neutrals-500);
  }

  &__name {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
  }

  &__meta {
    flex: none;
    font-size: 12px;
    color: var(--neutrals-500);
  }

  &__empty {
    padding: 8px;
    color: var(--neutrals-500);
  }
}
</style>
