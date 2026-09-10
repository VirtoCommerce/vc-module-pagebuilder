import { useApiClient } from "@vc-shell/framework";
import {
  PageBuilderSharedComponentsClient,
  type PageBuilderSharedComponent,
} from "../../../api_client/virtocommerce.pagebuildermodule";
import type { SharedComponent, SharedComponentSearchCriteria, SharedComponentSearchResult } from "../types";
import { getDistinctUsagePages, normalizeSharedComponentDeleteError } from "../utilities";

export function useSharedComponentsApi() {
  const { getApiClient } = useApiClient(PageBuilderSharedComponentsClient);

  async function searchSharedComponents(criteria: SharedComponentSearchCriteria): Promise<SharedComponentSearchResult> {
    const client = await getApiClient();
    const result = await client.search(criteria);

    return {
      totalCount: result.totalCount ?? result.results?.length ?? 0,
      results: (result.results ?? []).map(mapSharedComponent),
    };
  }

  async function getSharedComponent(id: string): Promise<SharedComponent> {
    const client = await getApiClient();
    return mapSharedComponent(await client.get(id));
  }

  async function renameSharedComponent(component: SharedComponent, name: string): Promise<SharedComponent> {
    const client = await getApiClient();
    const result = await client.update(component.id, {
      storeId: component.storeId,
      name,
    });

    return mapSharedComponent(result);
  }

  async function deleteSharedComponent(id: string): Promise<void> {
    const client = await getApiClient();

    try {
      await client.delete(id);
    } catch (error) {
      throw normalizeSharedComponentDeleteError(error, id);
    }
  }

  return {
    searchSharedComponents,
    getSharedComponent,
    renameSharedComponent,
    deleteSharedComponent,
  };
}

function mapSharedComponent(component: PageBuilderSharedComponent): SharedComponent {
  const usagePages = getDistinctUsagePages(component.usagePages);

  return {
    id: component.id ?? "",
    storeId: component.storeId ?? "",
    name: component.name ?? "",
    usageCount: component.usageCount ?? usagePages.length,
    usagePages,
    createdBy: component.createdBy,
    createdDate: component.createdDate,
    modifiedBy: component.modifiedBy,
    modifiedDate: component.modifiedDate,
  };
}
