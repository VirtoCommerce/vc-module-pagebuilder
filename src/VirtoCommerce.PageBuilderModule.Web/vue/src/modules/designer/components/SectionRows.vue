<template>
  <ul
    ref="list"
    class="pd-tree__list"
  >
    <li
      v-for="section in sections"
      :key="section.id"
      class="pd-tree__item"
    >
      <div
        class="pd-tree__row"
        :class="{
          'pd-tree__row--selected': isSelected(section.id),
          'pd-tree__row--hovered': designer.hovered.value === section.id,
          'pd-tree__row--hidden': section.hidden,
        }"
        role="button"
        tabindex="0"
        @click="select({ kind: 'section', id: section.id })"
        @keydown.enter="select({ kind: 'section', id: section.id })"
        @mouseenter="emit('hover', section.id)"
        @mouseleave="emit('hover', null)"
      >
        <input
          v-if="selecting"
          type="checkbox"
          class="pd-tree__check"
          :checked="designer.checked.value.includes(section.id)"
          :aria-label="sectionName(section)"
          @click.stop
          @change="designer.toggleChecked(section.id)"
        />
        <span
          v-else
          class="pd-tree__handle"
          :title="$t('PAGE_BUILDER.DESIGNER.DRAG')"
        >
          <VcIcon
            icon="lucide-grip-vertical"
            size="s"
          />
        </span>
        <button
          v-if="canHoldBlocks(section)"
          type="button"
          class="pd-tree__toggle"
          :aria-expanded="expanded.has(section.id)"
          :aria-label="$t('PAGE_BUILDER.DESIGNER.BLOCKS')"
          @click.stop="toggle(section.id)"
        >
          <VcIcon
            icon="lucide-chevron-right"
            size="xs"
            :class="{ 'pd-tree__chevron--open': expanded.has(section.id) }"
          />
        </button>
        <VcIcon
          :icon="isComponentRef(section) ? 'lucide-link' : iconOf(designer.sectionSchema(section.type))"
          size="s"
          class="pd-tree__icon"
        />
        <span class="pd-tree__name">{{ sectionName(section) }}</span>
        <span
          v-if="isComponentRef(section)"
          class="pd-tree__badge"
          :class="{ 'pd-tree__badge--missing': designer.components.get(section.componentRef) === null }"
          >{{ $t(designer.components.get(section.componentRef) === null ? "PAGE_BUILDER.DESIGNER.MISSING" : "PAGE_BUILDER.DESIGNER.SHARED") }}</span
        >
        <button
          v-else
          type="button"
          class="pd-tree__eye"
          :aria-label="$t(section.hidden ? 'PAGE_BUILDER.DESIGNER.SHOW' : 'PAGE_BUILDER.DESIGNER.HIDE')"
          :title="$t(section.hidden ? 'PAGE_BUILDER.DESIGNER.SHOW' : 'PAGE_BUILDER.DESIGNER.HIDE')"
          @click.stop="designer.toggleHidden({ kind: 'section', id: section.id })"
        >
          <VcIcon
            :icon="section.hidden ? 'lucide-eye-off' : 'lucide-eye'"
            size="s"
          />
        </button>
      </div>

      <ul
        v-if="canHoldBlocks(section) && expanded.has(section.id)"
        class="pd-tree__blocks"
      >
        <li
          v-for="block in section.blocks ?? []"
          :key="block.id"
        >
          <div
            class="pd-tree__row pd-tree__row--block"
            :class="{
              'pd-tree__row--selected': isSelected(block.id),
              'pd-tree__row--hidden': block.hidden,
            }"
            role="button"
            tabindex="0"
            @click="select({ kind: 'block', sectionId: section.id, id: block.id })"
            @keydown.enter="select({ kind: 'block', sectionId: section.id, id: block.id })"
          >
            <VcIcon
              :icon="iconOf(designer.blockSchema(block.type))"
              size="s"
              class="pd-tree__icon"
            />
            <span class="pd-tree__name">{{ itemTitle(block, designer.blockSchema(block.type)) }}</span>
            <!-- as in Shopify's tree: a block goes in one click (Undo brings it back) -->
            <button
              type="button"
              class="pd-tree__eye"
              :aria-label="$t('PAGE_BUILDER.DESIGNER.REMOVE_BLOCK')"
              :title="$t('PAGE_BUILDER.DESIGNER.REMOVE_BLOCK')"
              @click.stop="designer.remove({ kind: 'block', sectionId: section.id, id: block.id })"
            >
              <VcIcon
                icon="lucide-trash-2"
                size="s"
              />
            </button>
            <button
              type="button"
              class="pd-tree__eye"
              :aria-label="$t(block.hidden ? 'PAGE_BUILDER.DESIGNER.SHOW' : 'PAGE_BUILDER.DESIGNER.HIDE')"
              @click.stop="designer.toggleHidden({ kind: 'block', sectionId: section.id, id: block.id })"
            >
              <VcIcon
                :icon="block.hidden ? 'lucide-eye-off' : 'lucide-eye'"
                size="s"
              />
            </button>
          </div>
        </li>
        <li>
          <VcDropdown
            :model-value="blockMenu === section.id"
            :items="blockTypes(section)"
            :floating="true"
            placement="bottom-start"
            @update:model-value="(open: boolean) => (blockMenu = open ? section.id : undefined)"
            @item-click="(x: { type: string }) => addBlock(section.id, x.type)"
          >
            <template #trigger="{ isActive, toggle: open }">
              <button
                type="button"
                class="pd-tree__add pd-tree__add--block"
                :aria-expanded="isActive"
                @click="open"
              >
                <VcIcon
                  icon="lucide-circle-plus"
                  size="s"
                />
                {{ $t("PAGE_BUILDER.DESIGNER.ADD_BLOCK") }}
              </button>
            </template>
            <template #item="{ item }">
              <span class="pd-tree__menu-item">
                <VcIcon
                  :icon="iconOf(item.schema)"
                  size="s"
                />
                {{ item.schema.name ?? item.type }}
              </span>
            </template>
          </VcDropdown>
        </li>
      </ul>
    </li>
  </ul>
</template>

<script setup lang="ts">
// One list of the tree: the page's sections, or a section group's (the header's, the footer's). Each row has its
// blocks and "Add block"; drag a row by its handle to reorder (within this list).
import { nextTick, reactive, ref, useTemplateRef, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useSortable } from "@vueuse/integrations/useSortable";
import { VcDropdown, VcIcon } from "@vc-shell/framework/ui";
import type { Item, Selection } from "../types";
import type { Designer } from "../useDesigner";
import { iconOf, itemTitle } from "../schema";
import { isComponentRef } from "../document";

const props = defineProps<{
  designer: Designer;
  sections: Item[];
  /** where the list's first section is in the document (the sections before it are the other lists') */
  offset: number;
  /** select mode: checkboxes instead of handles */
  selecting?: boolean;
}>();
const emit = defineEmits<{ hover: [sectionId: string | null] }>();

const { t } = useI18n({ useScope: "global" });
const blockMenu = ref<string>();
const expanded = reactive(new Set<string>());

const sectionName = (section: Item) =>
  isComponentRef(section)
    ? (props.designer.components.get(section.componentRef)?.name ?? t("PAGE_BUILDER.DESIGNER.SHARED_COMPONENT"))
    : itemTitle(section, props.designer.sectionSchema(section.type));
const isSelected = (id: string) => {
  const sel = props.designer.selection.value;
  return !!sel && sel.kind !== "page" && sel.id === id;
};
const canHoldBlocks = (section: Item) => !isComponentRef(section) && !!props.designer.sectionSchema(section.type)?.blocks?.length;
const blockTypes = (section: Item) =>
  (props.designer.sectionSchema(section.type)?.blocks ?? [])
    .map((type) => ({ type, schema: props.designer.blockSchema(type) ?? { name: type } }))
    .filter((x) => !x.schema.hidden);

// the menu closes on a pick (it stays open on its own and covers the rows below)
function addBlock(sectionId: string, type: string) {
  blockMenu.value = undefined;
  props.designer.addBlock(sectionId, type);
}

function toggle(id: string) {
  if (expanded.has(id)) expanded.delete(id);
  else expanded.add(id);
}

function select(sel: Selection) {
  props.designer.selection.value = sel;
}

// a selected block's section opens; a new section with blocks too
watch(
  () => props.designer.selection.value,
  (sel) => {
    if (sel?.kind === "block" && props.sections.some((x) => x.id === sel.sectionId)) expanded.add(sel.sectionId);
  },
);

// drag sections by their handle; Sortable moves the DOM, the document is the truth: put the DOM back and move the data
const list = useTemplateRef<HTMLElement>("list");
useSortable(list, [], {
  handle: ".pd-tree__handle",
  animation: 150,
  onUpdate: (e: { oldIndex?: number; newIndex?: number; item: HTMLElement; from: HTMLElement }) => {
    if (e.oldIndex == null || e.newIndex == null) return;
    const { oldIndex, newIndex, item, from } = e;
    from.removeChild(item);
    from.insertBefore(item, from.children[oldIndex] ?? null);
    nextTick(() => props.designer.moveSection(props.offset + oldIndex, props.offset + newIndex));
  },
});
</script>
