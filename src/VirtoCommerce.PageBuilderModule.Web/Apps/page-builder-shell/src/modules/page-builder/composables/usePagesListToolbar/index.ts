import { computed, Ref, ShallowRef } from "vue";
import { useI18n } from "vue-i18n";
import { IBladeToolbar } from "@vc-shell/framework";
import { PageLifecycleFilters } from "../usePageBuilderList";
import { ExposedPagesList } from "../../components";

export function usePagesListToolbar(
  status: PageLifecycleFilters | null,
  pagesListRef: Readonly<ShallowRef<ExposedPagesList | null>>,
): Ref<IBladeToolbar[]> {
  const { t } = useI18n({ useScope: "global" });
  const isRemoveDisabled = computed(() => {
    const items = <string[]>(<unknown>pagesListRef.value?.selectedItems) || [];
    return (items.length || 0) === 0;
  });

  return computed(() => [
    {
      id: "add",
      icon: "material-add",
      title: t("PAGE_BUILDER.PAGES.LIST.TOOLBAR.ADD"),
      clickHandler: async () => {
        await pagesListRef.value?.openAddBlade();
      },
    },
    {
      id: "refresh",
      icon: "material-refresh",
      title: t("PAGE_BUILDER.PAGES.LIST.TOOLBAR.REFRESH"),
      clickHandler: async () => {
        await pagesListRef.value?.reload();
      },
    },
    {
      id: "delete",
      icon: "material-delete",
      title: t("PAGE_BUILDER.PAGES.LIST.TOOLBAR.REMOVE"),
      disabled: isRemoveDisabled,
      clickHandler: async () => {
        await pagesListRef.value?.removeSelectedPages();
      },
    },
  ]);
}
