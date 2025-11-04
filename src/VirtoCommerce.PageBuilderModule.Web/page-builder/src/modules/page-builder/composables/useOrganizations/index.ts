import { computed, Ref, ref } from "vue";
import { useApiClient } from "@vc-shell/framework";
import { PageBuilderPageClient } from "../../../../api_client/virtocommerce.pagebuildermodule";

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
  getOrganizations(): Promise<IOrganizationsResult>;
}

const { getApiClient } = useApiClient(PageBuilderPageClient);

export default (): IUseOrganizations => {
  const loading = ref(false);
  const types = ref<IOrganization[]>([]);

  async function getOrganizations(): Promise<IOrganizationsResult> {
    loading.value = true;
    const client = await getApiClient();
    try {
      const memberSearchResult = await client.getOrganizations();
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

  return {
    loading: computed(() => loading.value),
    types: computed(() => types.value),
    getOrganizations,
  };
};
