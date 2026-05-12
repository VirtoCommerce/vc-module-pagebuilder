import { computed } from "vue";
import { useAiAgentContext } from "@vc-shell/framework";
import useStoreParams from "../useStoreParams";

interface StoreSentinelItem {
  id: string | undefined;
  objectType: string | undefined;
  name: string | undefined;
  [key: string]: unknown;
}

const STORE_SENTINEL_OBJECT_TYPE = "pagebuilder.store";

// WORKAROUND for missing storeId in the AI agent context contract.
// See docs/storeId-missing-in-ai-context.md in the task root for the full write-up
// and the proper long-term fix (typed storeId field in vc-shell payload + virto-oz BaseChatContext).
// Until that lands, we smuggle storeId through items[] as a synthetic context item
// with objectType="pagebuilder.store" so the LLM can read it from the system message.
export default function useAiAgentStoreContext() {
  const { storeId, initUrlParams } = useStoreParams();
  initUrlParams();

  const items = computed<StoreSentinelItem[]>(() =>
    storeId.value
      ? [
          {
            id: storeId.value,
            objectType: STORE_SENTINEL_OBJECT_TYPE,
            name: `Current store: ${storeId.value}`,
          },
        ]
      : [],
  );

  return useAiAgentContext({ dataRef: items });
}
