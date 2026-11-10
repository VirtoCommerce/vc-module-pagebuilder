import { getBladeConfig, notification, useBlade } from "@vc-shell/framework";
import { useI18n } from "vue-i18n";
import { canOpenPageDesigner, openPageDesigner, type PageDesignerContext } from "../../../../utilities/pageDesigner";
import useUrlParams from "../useStoreParams";

export function usePageDesigner() {
  const { t } = useI18n({ useScope: "global" });
  const { openBlade } = useBlade();
  const { isNewPageBuilder } = useUrlParams();

  // A designer in another tab never reports back, so onOpen and onClose run only for the designer blade.
  return async (context: PageDesignerContext, { onOpen, onClose }: { onOpen?: () => void; onClose?: () => void } = {}) => {
    const newPageBuilder = await isNewPageBuilder(context.storeId);
    if (newPageBuilder === undefined) {
      notification.error(t("PAGE_BUILDER.DESIGNER.STORE_READ_FAILED"));
      return;
    }
    if (!newPageBuilder) {
      openPageDesigner(context, true);
      return;
    }
    if (!getBladeConfig("PageBuilderDesigner")) {
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
