import { computed, Ref, ShallowRef } from "vue";
import { useI18n } from "vue-i18n";
import { IBladeToolbar } from "@vc-shell/framework";
import { PageLifecycleFilters } from "../usePageBuilderList";
import { ExposedPagesList } from "../../components";
import { refreshMenuBadges } from "../usePageBuilderBadges";
import useUrlParams from "../useStoreParams";

export function usePagesListToolbar(
  _status: PageLifecycleFilters | null,
  pagesListRef: Readonly<ShallowRef<ExposedPagesList | null>>,
): Ref<IBladeToolbar[]> {
  const { t } = useI18n({ useScope: "global" });
  const { storeContextStatus } = useUrlParams();
  const isStoreContextReady = computed(() => storeContextStatus.value === "ready");
  const isRemoveDisabled = computed(() => {
    const items = (pagesListRef.value?.selectedItems as unknown as string[]) || [];
    return !isStoreContextReady.value || (items.length || 0) === 0;
  });

  return computed(() => {
    const items: IBladeToolbar[] = [
      {
        id: "add",
        icon: "lucide-plus",
        title: t("PAGE_BUILDER.PAGES.LIST.TOOLBAR.ADD"),
        disabled: computed(() => !isStoreContextReady.value),
        clickHandler: async () => {
          await pagesListRef.value?.openAddBlade();
        },
      },
      {
        id: "refresh",
        icon: "lucide-refresh-cw",
        title: t("PAGE_BUILDER.PAGES.LIST.TOOLBAR.REFRESH"),
        clickHandler: async () => {
          await pagesListRef.value?.reload();
          refreshMenuBadges();
        },
      },
      {
        id: "delete",
        icon: "lucide-trash-2",
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
        icon: "lucide-upload",
        title: t("PAGE_BUILDER.PAGES.LIST.TOOLBAR.LOAD"),
        disabled: computed(() => !isStoreContextReady.value),
        clickHandler: async () => {
          await pagesListRef.value?.openLoadFlow();
        },
      });
    }

    return items;
  });
}
