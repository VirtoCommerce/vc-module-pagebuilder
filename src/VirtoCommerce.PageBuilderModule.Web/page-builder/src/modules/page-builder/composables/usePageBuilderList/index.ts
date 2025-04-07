import { computed, ref, Ref, onMounted } from "vue";
import {
  ListComposableArgs,
  ListBaseBladeScope,
  useBladeNavigation,
  useListFactory,
  type TOpenBladeArgs,
  useApiClient,
} from "@vc-shell/framework";
import { useI18n } from "vue-i18n";

import useUrlParams from "../useUrlParams";

import {
  PageBuilderPageClient,
  IPageBuilderPageSearchCriteria,
  PageBuilderPageSearchCriteria,
  PageBuilderPage,
  GroupedPageBuilderPage,
} from "../../../../api_client/virtocommerce.pagebuildermodule";

const { getApiClient } = useApiClient(PageBuilderPageClient);

const { storeId, initUrlParams } = useUrlParams();

export enum PageStatuses {
  Draft = "Draft",
  Published = "Published",
  Archived = "Archived",
}

// eslint-disable-next-line @typescript-eslint/no-empty-interface
export interface DynamicItemsScope extends ListBaseBladeScope {}

export default (args: ListComposableArgs) => {
  initUrlParams();

  const listFactory = useListFactory<GroupedPageBuilderPage[], IPageBuilderPageSearchCriteria>({
    load: async (_query) => {
      if (!storeId?.value) {
        return {
          totalCount: 0,
          results: [],
        };
      }
      
      const criteria = { ...(_query || {}) } as PageBuilderPageSearchCriteria;
      criteria.storeId = storeId.value;

      return (await getApiClient()).searchGrouped(criteria);
    },
    remove: async (_query, customQuery) => {
      const ids = customQuery.ids;
      if (ids) {
        return (await getApiClient()).archiveGrouped(ids);
      }
    },
  });

  const { load, remove, items, pagination, loading, query } = listFactory({ sort: "modifiedDate:desc", pageSize: 20 });
  const { openBlade, resolveBladeByName } = useBladeNavigation();

  async function openDetailsBlade(data?: Omit<Parameters<typeof openBlade>["0"], "blade">) {
    await openBlade({
      blade: resolveBladeByName("PageBuilderDetails"),
      ...data,
      options: {
        storeId: storeId?.value ?? undefined,
      },
    });
  }

  const { t } = useI18n({ useScope: "global" });

  const scope: DynamicItemsScope = {
    openDetailsBlade,
    pageStatuses: computed(() =>
      Object.values(PageStatuses).map((value) => ({
        value,
        label: t(`PAGE_BUILDER.STATUS.${value.toUpperCase()}`),
      })),
    ),
  };

  onMounted(() => {
    initUrlParams()
  })

  return {
    items,
    load,
    remove,
    loading,
    pagination,
    query,
    scope,
  };
};
