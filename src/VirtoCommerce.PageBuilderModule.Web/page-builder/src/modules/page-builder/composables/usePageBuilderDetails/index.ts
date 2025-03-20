import { computed, ref, watch, reactive, Ref, onMounted } from "vue";
import { DetailsBaseBladeScope, IBladeToolbar, useDetailsFactory, DetailsComposableArgs, useApiClient } from "@vc-shell/framework";
import { useI18n } from "vue-i18n";

import {
  PageBuilderPageClient,
  //PageBuilderPage,
  GroupedPageBuilderPage,
} from "../../../../api_client/virtocommerce.pagebuildermodule";

import useCultureNames from "../useCultureNames";
import useUrlParams from "../useUrlParams";

const { getApiClient } = useApiClient(PageBuilderPageClient);
const { getCultureNames } = useCultureNames();
const { storeId, initUrlParams } = useUrlParams();

export interface DynamicItemScope extends DetailsBaseBladeScope {
  toolbarOverrides: {
    saveChanges: IBladeToolbar;
    remove: IBladeToolbar;
    previewPage: IBladeToolbar;
    openPageDesigner: IBladeToolbar;
    publishPage: IBladeToolbar;
    unpublishPage: IBladeToolbar;
  };
}

export default (args: DetailsComposableArgs<{ options: { sourceMessage: GroupedPageBuilderPage } }>) => {
  initUrlParams();

  let isNew = !args.props.param;
  
  let pageStoreId: string | undefined;
  if (args.props.options && args.props.options.storeId) {
    pageStoreId = args.props.options.storeId as string;
  } else {
    pageStoreId = storeId.value as string;
  }
  let newStatus: string | undefined;

  const detailsFactory = useDetailsFactory({
    load: async (page) => {
      if (page?.id) {
        return (await getApiClient()).getGrouped(page.id);
      }
    },
    saveChanges: async (page) => {
      const apiClient = await getApiClient();
      if (isNew) {
        page.status = "Draft";
        page.storeId = pageStoreId as string | undefined;
        return apiClient.createGrouped(page);
      } else {
        if (newStatus) {
          page.status = newStatus;
        }

        return apiClient.updateGrouped(page);
      }
    },
    remove: async ({ id }) => {
      if (id) {
        return (await getApiClient()).archiveGrouped([id]);
      }
    },
  });

  const { load, saveChanges, remove, loading, item, validationState } = detailsFactory();
  const contentType = "pages";

  const scope: DynamicItemScope = {
    toolbarOverrides: {
      saveChanges: {
        disabled: computed(() => !isEditable()),
      },
      remove: {
        isVisible: computed(() => !isNew),
        disabled: computed(() => !isEditable()),
      },
      previewPage: {
        clickHandler: async () => {
          throw new Error("Function not implemented.");
        },
        isVisible: computed(() => !isNew),
        disabled: computed(() => !validationState.value.valid),
      },
      openPageDesigner: {
        clickHandler: async () => {
          // Get platform URL from env
          const platformUrl = import.meta.env.APP_PLATFORM_URL?.replace(/\/$/, '') || window.location.origin;
          
          let designerUrl = platformUrl +
            (platformUrl.endsWith('/') ? '' : '/') +
            'Modules/$(VirtoCommerce.PageBuilderModule)/Content/builder/index.html'          

          let pageId = item.value?.id;
          let pageStoreId = item.value?.storeId;

          if (pageId && pageStoreId) {
            window.open(designerUrl + '?storeId=' + pageStoreId + '#/pages?type=' + contentType + '&pageId=' + pageId, '_blank');
          }
          else {
            throw new Error("Can't open page.");
          }
        },
        isVisible: computed(() => !isNew),
        disabled: computed(() => !validationState.value.valid || !isEditable()),
      },
      publishPage: {
        clickHandler: async () => {
          let pageId = item.value?.id;

          await (await getApiClient()).publishing(pageId, true);

          args.emit("parent:call", { method: "reload" });
          await load({ id: item.value?.id! })   
        },
        isVisible: computed(() => !isNew && item.value?.status != "Published"),
        disabled: computed(() => !validationState.value.valid || !isEditable()),
      },
      unpublishPage: {
        clickHandler: async () => {
          // check if the page has changes
          if (item.value?.hasChanges) {
            throw new Error(t("PAGE_BUILDER.PAGES.ALERTS.UNPUBLISH_WITH_DRAFT"));
          }

          let pageId = item.value?.id;

          await (await getApiClient()).publishing(pageId, false);

          args.emit("parent:call", { method: "reload" }); 
          await load({ id: item.value?.id! })
        },
        isVisible: computed(() => !isNew && item.value?.status == "Published"),
        disabled: computed(() => !validationState.value.valid || !isEditable()),
      },
    },
    loadCultureNames: async() => {
      return getCultureNames();
    },
    isReadOnly: () => !isEditable(),
    statusText: computed(() => {
      let result = "Draft";
      const page = item.value;
      if (page == null) {
        return result;
      }

      return page.status;
    }),
  };

  function isEditable(): boolean {
    return item.value != null && item.value.status !== "Archived";
  }

  const { t } = useI18n({ useScope: "global" });

  const bladeTitle = computed(() => {
    return isNew
      ? item.value?.name
        ? item.value?.name + t("PAGE_BUILDER.PAGES.DETAILS.TITLE.DETAILS")
        : t("PAGE_BUILDER.PAGES.DETAILS.TITLE.NEW")
      : item.value?.name + t("PAGE_BUILDER.PAGES.DETAILS.TITLE.DETAILS");
  });

  watch(
    () => args?.mounted.value,
    async () => {
      if (isNew) {
        const page = new GroupedPageBuilderPage();
        item.value = reactive(page);
        validationState.value.resetModified(item.value, true);
      }
    },
  );

  onMounted(() => {
    initUrlParams()
  })

  return {
    load,
    saveChanges,
    remove,
    loading,
    item,
    validationState,
    bladeTitle,
    scope,
  };
};
