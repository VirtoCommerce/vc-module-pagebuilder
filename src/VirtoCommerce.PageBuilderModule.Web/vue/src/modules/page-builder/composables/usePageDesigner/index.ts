import { getBladeConfig, useBlade } from "@vc-shell/framework";
import { canOpenPageDesigner, openPageDesigner, type PageDesignerContext } from "../../../../utilities/pageDesigner";
import useUrlParams from "../useStoreParams";

export function usePageDesigner() {
  const { openBlade } = useBlade();
  const { isNewPageBuilder } = useUrlParams();

  return async (context: PageDesignerContext, onClose?: () => void) => {
    if (!(await isNewPageBuilder(context.storeId))) {
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
    openBlade({ name: "PageBuilderDesigner", param: context.groupId, onClose });
  };
}

export function useSharedComponentDesigner() {
  const { openBlade } = useBlade();

  return (storeId: string, sharedComponentId: string, onClose?: () => void) =>
    openBlade({ name: "PageBuilderDesigner", query: { storeId, sharedComponentId }, onClose });
}
