import { computed, watch, reactive, onMounted } from "vue";
import {
  DetailsBaseBladeScope,
  IBladeToolbar,
  useDetailsFactory,
  DetailsComposableArgs,
  useApiClient,
} from "@vc-shell/framework";
import { useI18n } from "vue-i18n";

import {
  PageBuilderPageClient,
  //PageBuilderPage,
  GroupedPageBuilderPage,
} from "../../../../api_client/virtocommerce.pagebuildermodule";

import useCultureNames from "../useCultureNames";
import useUserGroups from "../useUserGroups";
import useUrlParams from "../useUrlParams";

const { getApiClient } = useApiClient(PageBuilderPageClient);
const { getCultureNames } = useCultureNames();
const { getUserGroups } = useUserGroups();
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

interface ExtendedGroupedPageBuilderPage extends GroupedPageBuilderPage {
  visibility?: boolean;
  userGroups?: string[];
  startDate?: Date;
  endDate?: Date;
  pageContent?: string;
  newPageContent?: string;
}

export default (args: DetailsComposableArgs<{ options: { sourceMessage: GroupedPageBuilderPage } }>) => {
  initUrlParams();

  const isNew = !args.props.param;

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
        const apiClient = await getApiClient();
        const result = (await apiClient.getGrouped(page.id)) as ExtendedGroupedPageBuilderPage;
        try {
          if (result.pageContent) {
            const model = JSON.parse(result.pageContent);
            result.visibility = model.settings.visibility;
            result.userGroups = model.settings.userGroups?.split(",") || [];
            result.startDate = model.settings.startDate;
            result.endDate = model.settings.endDate;
          }
        } catch (e) {
          console.error(e);
        }
        return result;
      }
    },
    saveChanges: async (page) => {
      const apiClient = await getApiClient();
      const pageContent = page.pageContent ? JSON.parse(page.pageContent) : { settings: {}, content: [] };
      const newSettings = {
        visibility: page.visibility,
        userGroups: page.userGroups?.filter(x => !!x).join(","),
        cultureName: page.cultureName,
        startDate: page.startDate,
        endDate: page.endDate,
        permalink: page.permalink,
        name: page.name,
      };

      pageContent.settings = { ...pageContent.settings, ...newSettings };
      page.newPageContent = JSON.stringify(pageContent);

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
        disabled: computed(() => !validationState.value.modified || !validationState.value.valid),
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
          const platformUrl: string = ((import.meta.env.DEV && import.meta.env.APP_PLATFORM_URL) || window.location.origin).replace(/\/$/, "");
          const designerUrl = `${platformUrl}/Modules/$(VirtoCommerce.PageBuilderModule)/Content/builder/index.html`;
          const pageId = item.value?.id;
          const pageStoreId = item.value?.storeId;

          if (pageId && pageStoreId) {
            const url = `${designerUrl}?storeId=${pageStoreId}#/pages?type=${contentType}&pageId=${pageId}`;
            window.open(url, "_blank");
          } else {
            throw new Error("Can't open page.");
          }
        },
        isVisible: computed(() => !isNew),
        disabled: computed(() => !validationState.value.valid || !isEditable()),
      },
      publishPage: {
        clickHandler: async () => {
          const pageId = item.value?.id;
          const apiClient = await getApiClient();

          await apiClient.publishing(pageId, true);

          if (item.value) {
            args.emit("parent:call", { method: "reload" });
            await load({ id: item.value.id! });
          }
        },
        isVisible: computed(() => !isNew && item.value?.hasChanges == true),
        disabled: computed(() => !validationState.value.valid || !isEditable()),
      },
      unpublishPage: {
        clickHandler: async () => {
          // check if the page has changes
          if (item.value?.hasChanges) {
            throw new Error(t("PAGE_BUILDER.PAGES.ALERTS.UNPUBLISH_WITH_DRAFT"));
          }

          const pageId = item.value?.id;
          const apiClient = await getApiClient();

          await apiClient.publishing(pageId, false);

          if (item.value) {
            args.emit("parent:call", { method: "reload" });
            await load({ id: item.value.id! });
          }
        },
        isVisible: computed(() => !isNew && item.value?.hasChanges == false),
        disabled: computed(() => !validationState.value.valid || !isEditable()),
      },
    },
    loadCultureNames: async () => {
      return getCultureNames(pageStoreId);
    },
    loadUserGroups: async () => {
      return getUserGroups();
    },
    isReadOnly: () => !isEditable(),
    statusText: computed(() => {
      const result = "Draft";
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
    initUrlParams();
  });

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
