import { useApiClient } from "@vc-shell/framework";
import {
  PageBuilderLinkedComponentsClient,
  type PageBuilderLinkedComponent,
} from "../../../api_client/virtocommerce.pagebuildermodule";
import type { LinkedComponent, LinkedComponentSearchCriteria, LinkedComponentSearchResult } from "../types";
import { getDistinctUsagePages, normalizeLinkedComponentDeleteError } from "../utilities";

export function useLinkedComponentsApi() {
  const { getApiClient } = useApiClient(PageBuilderLinkedComponentsClient);

  async function searchLinkedComponents(criteria: LinkedComponentSearchCriteria): Promise<LinkedComponentSearchResult> {
    const client = await getApiClient();
    const result = await client.search(criteria);

    return {
      totalCount: result.totalCount ?? result.results?.length ?? 0,
      results: (result.results ?? []).map(mapLinkedComponent),
    };
  }

  async function getLinkedComponent(id: string): Promise<LinkedComponent> {
    const client = await getApiClient();
    return mapLinkedComponent(await client.get(id));
  }

  async function renameLinkedComponent(component: LinkedComponent, name: string): Promise<LinkedComponent> {
    const client = await getApiClient();
    const result = await client.update(component.id, {
      storeId: component.storeId,
      name,
    });

    return mapLinkedComponent(result);
  }

  async function deleteLinkedComponent(id: string): Promise<void> {
    const client = await getApiClient();

    try {
      await client.delete(id);
    } catch (error) {
      throw normalizeLinkedComponentDeleteError(error, id);
    }
  }

  return {
    searchLinkedComponents,
    getLinkedComponent,
    renameLinkedComponent,
    deleteLinkedComponent,
  };
}

function mapLinkedComponent(component: PageBuilderLinkedComponent): LinkedComponent {
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
