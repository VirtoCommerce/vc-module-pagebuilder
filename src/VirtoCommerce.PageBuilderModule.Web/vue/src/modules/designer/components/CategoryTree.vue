<template>
  <div class="pd-cats">
    <ul
      class="pd-cats__list"
      role="tree"
      :aria-label="label"
    >
      <li
        v-for="{ node, depth, covered } in rows"
        :key="node.id"
        role="treeitem"
        class="pd-cats__row"
        :style="{ paddingLeft: `${depth * 16 + 4}px` }"
        :aria-expanded="node.hasChildren ? expanded.has(node.id) : undefined"
      >
        <button
          v-if="node.hasChildren"
          type="button"
          class="pd-cats__toggle"
          :aria-label="node.name"
          @click="toggle(node.id)"
        >
          <VcIcon
            icon="lucide-chevron-right"
            size="xs"
            :class="{ 'pd-cats__chevron--open': expanded.has(node.id) }"
          />
        </button>
        <span
          v-else
          class="pd-cats__toggle"
        />
        <label class="pd-cats__label">
          <input
            type="checkbox"
            :checked="covered || modelValue.includes(node.id)"
            :disabled="covered"
            @change="check(node.id)"
          />
          <span>{{ node.name }}</span>
        </label>
      </li>
      <li
        v-if="loading.has('')"
        class="pd-cats__note"
      >
        <VcIcon
          icon="lucide-loader-circle"
          size="s"
          class="pd-cats__spin"
        />
      </li>
      <li
        v-else-if="failed"
        class="pd-cats__note"
      >
        {{ $t("PAGE_BUILDER.DESIGNER.RULES.TREE_FAILED") }}
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
// The store's category tree, loaded a level at a time, to pick the categories a rule is for. A rule's category holds
// for its subcategories too, so those show checked under a picked one.
import { computed, onMounted, reactive, ref } from "vue";
import { VcIcon } from "@vc-shell/framework/ui";
import { childCategories, type CatalogContext, type NamedEntity } from "../api";

type Node = NamedEntity & { hasChildren: boolean };

const props = defineProps<{
  catalog: CatalogContext;
  modelValue: string[];
  /** the names of the categories shown, kept for the rules' descriptions */
  names: Map<string, string>;
  label?: string;
}>();
const emit = defineEmits<{ "update:modelValue": [ids: string[]] }>();

/** each category's subcategories, as loaded ("" the top ones) */
const children = reactive(new Map<string, Node[]>());
const parents = new Map<string, string>();
const expanded = reactive(new Set<string>());
const loading = reactive(new Set<string>());
const failed = ref(false);

async function load(id: string) {
  if (children.has(id) || loading.has(id)) return;
  loading.add(id);
  try {
    const list = await childCategories(props.catalog, id || undefined);
    for (const node of list) {
      parents.set(node.id, id);
      props.names.set(node.id, node.name);
    }
    children.set(id, list);
  } catch {
    failed.value = true;
  } finally {
    loading.delete(id);
  }
}
onMounted(() => load(""));

function toggle(id: string) {
  if (expanded.has(id)) expanded.delete(id);
  else {
    expanded.add(id);
    void load(id);
  }
}

const isCovered = (id: string): boolean => {
  const parent = parents.get(id);
  return !!parent && (props.modelValue.includes(parent) || isCovered(parent));
};

const rows = computed(() => {
  const out: { node: Node; depth: number; covered: boolean }[] = [];
  const walk = (id: string, depth: number) => {
    for (const node of children.get(id) ?? []) {
      out.push({ node, depth, covered: isCovered(node.id) });
      if (expanded.has(node.id)) walk(node.id, depth + 1);
    }
  };
  walk("", 0);
  return out;
});

function check(id: string) {
  emit("update:modelValue", props.modelValue.includes(id) ? props.modelValue.filter((x) => x !== id) : [...props.modelValue, id]);
}
</script>

<style lang="scss" scoped>
.pd-cats {
  max-height: 240px;
  overflow-y: auto;
  border: 1px solid var(--neutrals-200);
  border-radius: 8px;

  &__list {
    margin: 0;
    padding: 4px;
    list-style: none;
  }

  &__row {
    display: flex;
    align-items: center;
    gap: 2px;
    min-height: 28px;
    border-radius: 6px;

    &:hover {
      background: var(--neutrals-100);
    }
  }

  &__toggle {
    flex: none;
    display: flex;
    align-items: center;
    justify-content: center;
    width: 20px;
    height: 20px;
    padding: 0;
    border: 0;
    background: none;
    color: var(--neutrals-500);
    cursor: pointer;
  }

  &__chevron--open {
    transform: rotate(90deg);
  }

  &__label {
    flex: 1;
    display: flex;
    align-items: center;
    gap: 6px;
    min-width: 0;
    cursor: pointer;

    input {
      flex: none;
      margin: 0;
      accent-color: var(--primary-500);
    }

    span {
      overflow: hidden;
      white-space: nowrap;
      text-overflow: ellipsis;
    }
  }

  &__note {
    display: flex;
    justify-content: center;
    padding: 8px;
    color: var(--neutrals-500);
  }

  &__spin {
    animation: pd-cats-spin 1s linear infinite;
  }
}

@keyframes pd-cats-spin {
  to {
    transform: rotate(360deg);
  }
}
</style>
