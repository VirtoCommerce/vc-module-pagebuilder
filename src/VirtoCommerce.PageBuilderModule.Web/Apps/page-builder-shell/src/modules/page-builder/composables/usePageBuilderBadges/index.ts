import { ref } from "vue";
import { useApiClient, setMenuBadge } from "@vc-shell/framework";
import {
  PageBuilderPageClient,
  PageBuilderPageSearchCriteria,
} from "../../../../api_client/virtocommerce.pagebuildermodule";
import { PageLifecycleFilters } from "../usePageBuilderList";
import useUrlParams from "../useStoreParams";

const { getApiClient } = useApiClient(PageBuilderPageClient);

const draftCount = ref<number | undefined>(undefined);
const pendingCount = ref<number | undefined>(undefined);
const activeCount = ref<number | undefined>(undefined);

setMenuBadge("DraftPagesList", { content: draftCount, variant: "warning" });
setMenuBadge("PendingPagesList", { content: pendingCount, variant: "info" });
setMenuBadge("ActivePagesList", { content: activeCount, variant: "success" });

export async function refreshMenuBadges(): Promise<void> {
  const { storeId, initUrlParams } = useUrlParams();
  initUrlParams();

  if (!storeId.value) return;

  const apiClient = await getApiClient();

  const [draftResult, pendingResult, activeResult] = await Promise.all([
    apiClient.searchGroups(
      new PageBuilderPageSearchCriteria({ storeId: storeId.value, take: 0, lifecycle: PageLifecycleFilters.Draft }),
    ),
    apiClient.searchGroups(
      new PageBuilderPageSearchCriteria({ storeId: storeId.value, take: 0, lifecycle: PageLifecycleFilters.Pending }),
    ),
    apiClient.searchGroups(
      new PageBuilderPageSearchCriteria({ storeId: storeId.value, take: 0, lifecycle: PageLifecycleFilters.Active }),
    ),
  ]);

  draftCount.value = draftResult.totalCount || undefined;
  pendingCount.value = pendingResult.totalCount || undefined;
  activeCount.value = activeResult.totalCount || undefined;
}
