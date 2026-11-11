import { computed, inject, ref, watch, type ComputedRef, type Ref } from "vue";
import {
  BladeDescriptorKey,
  BladeStackKey,
  useAiAgentContext,
  type UseAiAgentContextOptions,
} from "@vc-shell/framework";
import useUrlParams from "../useStoreParams";

const STORE_SENTINEL_OBJECT_TYPE = "pagebuilder.store";

interface ContextItem {
  id: string | undefined;
  objectType: string | undefined;
  name?: string | undefined;
  [key: string]: unknown;
}

export interface UseAiAgentContextWithStoreOptions<T extends ContextItem> {
  /** A single object is wrapped in an array: merged with the store sentinel, vc-shell's list mode trims its fields. */
  dataRef?: Ref<T | T[] | null | undefined> | ComputedRef<T | T[] | null | undefined>;
  suggestions?: UseAiAgentContextOptions<T>["suggestions"];
}

// Workaround: the AI agent contract has no storeId, so it travels as a sentinel context item.
// vc-shell's context manager keeps one slot per blade, so two useAiAgentContext calls in a blade would race for it.
export default function useAiAgentContextWithStore<T extends ContextItem = ContextItem>(
  options: UseAiAgentContextWithStoreOptions<T> = {},
): void {
  const bladeStack = inject(BladeStackKey, null);
  const bladeDescriptor = inject(BladeDescriptorKey, null);
  const { storeId } = useUrlParams();

  const ownBladeId = computed(() => bladeDescriptor?.value?.id);
  const isActiveBlade = computed(
    () => ownBladeId.value != null && bladeStack?.activeBlade.value?.id === ownBladeId.value,
  );

  const mergedItems = ref<ContextItem[]>([]);

  const dataRefSource: Ref<T | T[] | null | undefined> | ComputedRef<T | T[] | null | undefined> =
    options.dataRef ?? ref(undefined);

  watch(
    [isActiveBlade, storeId, dataRefSource],
    () => {
      if (!isActiveBlade.value) {
        // vc-shell writes to the active blade's slot, so a write from an inactive blade would land in another blade's slot.
        return;
      }

      const data = dataRefSource.value;
      const real: ContextItem[] = Array.isArray(data) ? [...data] : [data as ContextItem].filter(Boolean);

      if (!storeId.value) {
        mergedItems.value = real;
        return;
      }

      const sentinel: ContextItem = {
        id: storeId.value,
        objectType: STORE_SENTINEL_OBJECT_TYPE,
        name: `Current store: ${storeId.value}`,
      };

      mergedItems.value = [sentinel, ...real];
    },
    // A closing child blade's onUnmounted clears this blade's slot during the patch phase, so the re-push must run after it.
    { immediate: true, deep: true, flush: "post" },
  );

  useAiAgentContext({
    dataRef: mergedItems,
    suggestions: options.suggestions,
  });
}
