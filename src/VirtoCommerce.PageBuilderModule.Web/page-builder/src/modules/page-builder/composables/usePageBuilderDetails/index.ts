import { computed, ref, watch, reactive, Ref } from "vue";
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
const { storeId } = useUrlParams();

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
  let isNew = !args.props.param;
  let storeId = args.props.options.storeId;
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
        page.storeId = storeId as string | undefined;
        return apiClient.createGrouped(page);
      } else {
        if (newStatus) {
          page.status = newStatus;
        }

        const clonedPage = new GroupedPageBuilderPage();
        Object.assign(clonedPage, page);
        // Page Id will change after update since it's a composite key so we need to preemptively update it here 
        page.id = `${page.storeId}:${page.name}:${page.cultureName}:${page.permalink}`;
        return apiClient.updateGrouped(clonedPage);
      }
    },
    remove: async ({ id }) => {
      if (id) {
        return (await getApiClient()).archiveGrouped([id]);
      }
    },
  });

  const { load, saveChanges, remove, loading, item, validationState } = detailsFactory();

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

          let contentType = "pages";
          let pageId = item.value?.id;
          let storeId = item.value?.storeId;

          if (pageId && storeId) {
            window.open(designerUrl + '?storeId=' + storeId + '#/pages?type=' + contentType + '&pageId=' + pageId, '_blank');
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
          throw new Error("Function not implemented.");
        },
        isVisible: computed(() => !isNew && item.value?.status != "Published"),
        disabled: computed(() => !validationState.value.valid || !isEditable()),
      },
      unpublishPage: {
        clickHandler: async () => {
          throw new Error("Function not implemented.");
        },
        isVisible: computed(() => !isNew && item.value?.status == "Published"),
        disabled: computed(() => !validationState.value.valid || !isEditable()),
      },
    },
    loadCultureNames: async() => {
      return getCultureNames();
    },
    isReadOnly: () => !isEditable(),
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

        /*
        const sourceMessage = args.props.options?.sourceMessage;
        if (sourceMessage) {
          message.topic = sourceMessage.topic;
          message.shortMessage = sourceMessage.shortMessage;
          message.memberIds = sourceMessage.memberIds;
          message.memberQuery = sourceMessage.memberQuery;
        }
        */
      }
    },
  );


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
