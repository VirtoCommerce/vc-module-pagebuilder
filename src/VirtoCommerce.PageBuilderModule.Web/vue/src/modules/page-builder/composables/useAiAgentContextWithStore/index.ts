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
  /**
   * The blade's real data. If a single object is passed it gets wrapped in an
   * array before being merged with the store sentinel, which switches the
   * context into "list" mode and trims item fields down to {id, objectType,
   * name} per the useAiAgentContext contract. Merging a full "details" object
   * together with the sentinel is not possible under the current vc-shell
   * contract — that is a framework limitation, not a limitation of this wrapper.
   */
  dataRef?: Ref<T | T[] | null | undefined> | ComputedRef<T | T[] | null | undefined>;
  suggestions?: UseAiAgentContextOptions<T>["suggestions"];
}

// WORKAROUND for the missing storeId in the AI agent contract.
// See docs/storeId-missing-in-ai-context.md — the long-term fix is a typed
// storeId field in the vc-shell payload + virto-oz BaseChatContext.
//
// This wrapper solves four problems at once:
//  1. The storeId source is reactive and independent of the router: the store
//     context (useStoreParams), i.e. ?storeId= in the standalone app and the
//     host blade's store in the Admin Shell.
//  2. One writer per blade — the vc-shell context-manager keeps a SINGLE slot
//     per blade, so any two useAiAgentContext calls in the same blade race for
//     it. Every pagebuilder blade must go through this wrapper.
//  3. Active-blade gating. vc-shell's _setContextData always targets
//     bladeGetter()?.id — i.e. the CURRENTLY active blade, not "its own owner".
//     Writing while the blade is inactive would land in someone else's slot, so
//     we only update mergedItems while isActiveBlade === true.
//  4. Slot restoration after the framework's onUnmounted clobber. When a child
//     blade closes, its useAiAgentContext.onUnmounted calls _setContextData([])
//     while the parent is already active — deleting the parent's slot. When
//     focus returns to our blade (isActiveBlade flips false→true) the watch
//     fires again and re-writes the sentinel into the (now active again) slot.
//     IMPORTANT: the watch runs on flush: "post". A child blade's onUnmounted
//     fires during the patch phase (between the pre- and post-flush), so a
//     re-push on the pre-flush races the clobber and loses. The post-flush is
//     guaranteed to run AFTER unmount, so the restoration deterministically
//     overwrites the clobber. This is needed until the consumed @vc-shell/framework
//     build picks up the root targetBladeId fix in context-manager.setContextData
//     (after which the clobber can no longer touch another blade's slot).
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

  // Turn the optional dataRef into a stable source for the watch so the watch
  // dependency array keeps a constant shape.
  const dataRefSource: Ref<T | T[] | null | undefined> | ComputedRef<T | T[] | null | undefined> =
    options.dataRef ?? ref(undefined);

  watch(
    [isActiveBlade, storeId, dataRefSource],
    () => {
      if (!isActiveBlade.value) {
        // Leave mergedItems untouched: otherwise useAiAgentContext.watch would
        // fire and the write would land in the currently active blade's slot
        // (not ours).
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
    // flush: "post" — see point 4 in the header: the re-push must run after the
    // patch phase, where the closing child blade's onUnmounted clobber fires.
    { immediate: true, deep: true, flush: "post" },
  );

  useAiAgentContext({
    dataRef: mergedItems,
    suggestions: options.suggestions,
  });
}
