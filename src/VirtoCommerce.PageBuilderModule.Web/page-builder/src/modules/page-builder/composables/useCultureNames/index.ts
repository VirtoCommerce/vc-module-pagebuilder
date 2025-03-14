import { computed, Ref, ref } from "vue";
import { orderBy, sortBy } from "lodash-es";
import { useApiClient } from "@vc-shell/framework";

import {
  PageBuilderPageClient,
} from "../../../../api_client/virtocommerce.pagebuildermodule";

interface ICultureName {
  name: string;
}

interface ICultureNameResult {
  totalCount?: number;
  results?: ICultureName[];
}

interface IUseCultureNames {
  readonly loading: Ref<boolean>;
  readonly types: Ref<ICultureName[]>;
  getCultureNames(): Promise<ICultureNameResult>;
}

const { getApiClient } = useApiClient(PageBuilderPageClient);

export default (): IUseCultureNames => {
  const loading = ref(false);
  const types = ref<ICultureName[]>([]);

  async function getCultureNames(): Promise<ICultureNameResult> {
    loading.value = true;
    const client = await getApiClient();
    try {
      const languages = await client.getAvailableLanguages();
      const result = languages.map(lang => ({ name: lang }));
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
    getCultureNames,
  };
};
