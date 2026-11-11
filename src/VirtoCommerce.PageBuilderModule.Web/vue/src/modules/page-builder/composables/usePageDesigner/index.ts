import { notification, useBlade } from "@vc-shell/framework";
import { ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { canOpenPageDesigner, openPageDesigner, type PageDesignerContext } from "../../../../utilities/pageDesigner";
import useUrlParams from "../useStoreParams";

export function usePageDesigner() {
  const { t } = useI18n({ useScope: "global" });
  const { openBlade } = useBlade();
  const { storeId, isNewPageBuilder } = useUrlParams();

  // Read before the click: the browser blocks a tab opened after the platform answers as a pop-up.
  const known = ref<{ storeId: string; newPageBuilder: boolean }>();
  watch(
    storeId,
    async (id) => {
      known.value = undefined;
      const newPageBuilder = id ? await isNewPageBuilder(id) : undefined;
      if (id && id === storeId.value && newPageBuilder !== undefined) known.value = { storeId: id, newPageBuilder };
    },
    { immediate: true },
  );

  // A designer in another tab never reports back, so the callbacks run only for the designer blade.
  return async (context: PageDesignerContext, { onOpen, onClose }: { onOpen?: () => void; onClose?: () => void } = {}) => {
    const ahead = known.value;
    const newPageBuilder = ahead && ahead.storeId === context.storeId ? ahead.newPageBuilder : await isNewPageBuilder(context.storeId);
    if (newPageBuilder === undefined) {
      notification.error(t("PAGE_BUILDER.DESIGNER.STORE_READ_FAILED"));
      return;
    }
    if (!newPageBuilder) {
      openPageDesigner(context);
      return;
    }
    if (!canOpenPageDesigner(context)) {
      throw new Error("Can't open page.");
    }
    openBlade({ name: "PageBuilderDesigner", param: context.groupId, onOpen, onClose });
  };
}

export function useSharedComponentDesigner() {
  const { openBlade } = useBlade();

  return (storeId: string, sharedComponentId: string, onClose?: () => void) =>
    openBlade({ name: "PageBuilderDesigner", query: { storeId, sharedComponentId }, onClose });
}
