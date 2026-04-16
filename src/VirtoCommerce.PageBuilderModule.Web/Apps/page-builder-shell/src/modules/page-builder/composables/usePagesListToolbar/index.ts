import { computed, Ref, ShallowRef } from "vue";
import { useI18n } from "vue-i18n";
import { IBladeToolbar } from "@vc-shell/framework";
import { PageLifecycleFilters } from "../usePageBuilderList";
import { ExposedPagesList } from "../../components";
import { refreshMenuBadges } from "../usePageBuilderBadges";

export function usePagesListToolbar(
  _status: PageLifecycleFilters | null,
  pagesListRef: Readonly<ShallowRef<ExposedPagesList | null>>,
): Ref<IBladeToolbar[]> {
  const { t } = useI18n({ useScope: "global" });
  const isRemoveDisabled = computed(() => {
    const items = <string[]>(<unknown>pagesListRef.value?.selectedItems) || [];
    return (items.length || 0) === 0;
  });

  return computed(() => {
    const items: IBladeToolbar[] = [
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
          refreshMenuBadges();
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
    ];

    if (_status === PageLifecycleFilters.Draft) {
      items.push({
        id: "load",
        icon: "material-upload",
        title: t("PAGE_BUILDER.PAGES.LIST.TOOLBAR.LOAD"),
        clickHandler: async () => {
          await pagesListRef.value?.openLoadFlow();
        },
      });
    }

    return items;
  });
}
