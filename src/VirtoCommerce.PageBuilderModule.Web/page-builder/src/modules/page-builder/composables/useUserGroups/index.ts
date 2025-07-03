import { computed, Ref, ref } from "vue";
import { useApiClient } from "@vc-shell/framework";
import { PageBuilderPageClient } from "../../../../api_client/virtocommerce.pagebuildermodule";

export interface IUserGroup {
  name: string;
}

export interface IUserGroupsResult {
  totalCount?: number;
  results?: IUserGroup[];
}

export interface IUseUserGroups {
  readonly loading: Ref<boolean>;
  readonly types: Ref<IUserGroup[]>;
  getUserGroups(): Promise<IUserGroupsResult>;
}

const { getApiClient } = useApiClient(PageBuilderPageClient);

export default (): IUseUserGroups => {
  const loading = ref(false);
  const types = ref<IUserGroup[]>([]);

  async function getUserGroups(): Promise<IUserGroupsResult> {
    loading.value = true;
    const client = await getApiClient();
    try {
      const userGroups = await client.getUserGroups();
      const result = userGroups.map((userGroup) => ({ name: userGroup }));
      types.value = result;
      return {
        totalCount: types.value.length,
        results: types.value,
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
    getUserGroups,
  };
};
