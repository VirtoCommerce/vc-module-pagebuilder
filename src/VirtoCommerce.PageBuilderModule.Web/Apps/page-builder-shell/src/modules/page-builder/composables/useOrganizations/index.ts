import { computed, Ref, ref } from "vue";
import { useApiClient } from "@vc-shell/framework";
import { PageBuilderPageSettingsClient, MembersSearchCriteria } from "../../../../api_client/virtocommerce.pagebuildermodule";

export interface IOrganization {
  name: string;
  id: string;
}

export interface IOrganizationsResult {
  results: IOrganization[];
  total: number;
}

export interface IUseOrganizations {
  readonly loading: Ref<boolean>;
  readonly types: Ref<IOrganization[]>;
  getOrganizations(keyword?: string, skip?: number, ids?: string[]): Promise<IOrganizationsResult>;
  getOrganization(id: string): Promise<string | null>;
}

const cache = new Map<string, IOrganization>();

const { getApiClient } = useApiClient(PageBuilderPageSettingsClient);

export default (): IUseOrganizations => {
  const loading = ref(false);
  const types = ref<IOrganization[]>([]);

  async function getOrganizations(
    keyword?: string,
    skip?: number,
    objectIds?: string[],
  ): Promise<IOrganizationsResult> {
    loading.value = true;
    const client = await getApiClient();
    try {
      const memberSearchResult = await client.getOrganizations(
        new MembersSearchCriteria({ keyword, skip: skip ?? 0, objectIds }),
      );
      const result =
        memberSearchResult.results?.map((organization) => ({
          name: organization.name ?? organization.id!,
          id: organization.id!,
        })) || [];
      types.value = result;
      return {
        results: result,
        total: memberSearchResult.totalCount || 0,
      };
    } catch (e) {
      console.error(e);
      throw e;
    } finally {
      loading.value = false;
    }
  }

  async function getOrganization(id: string): Promise<string | null> {
    if (cache.has(id)) {
      return cache.get(id)!.name;
    }
    loading.value = true;
    const client = await getApiClient();
    try {
      const organization = await client.getOrganization(id);
      if (organization) {
        const result = {
          name: organization.name || organization.id!,
          id: organization.id!,
        };
        cache.set(id, result);
        return result.name;
      }
      return null;
    } catch (e) {
      console.error(e);
      throw e;
    } finally {
      loading.value = false;
    }
  }

  return {
    loading: computed(() => loading.value),
    types: computed(() => types.value),
    getOrganizations,
    getOrganization,
  };
};
