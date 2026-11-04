import { getBladeConfig, useBlade } from "@vc-shell/framework";
import { canOpenPageDesigner, openPageDesigner, type PageDesignerContext } from "../../../../utilities/pageDesigner";
import useUrlParams from "../useStoreParams";

/**
 * Opens a page in the designer. A store without the New Page Builder switch opens the Angular designer in a new tab.
 * Otherwise the Admin Shell opens the full-screen PageBuilderDesigner blade (param = page group id), and the standalone
 * app, which doesn't register that blade, opens the Vue designer in a new tab.
 */
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

/**
 * Opens a shared component in the designer: its sections, edited once for every page that places it. The Vue designer
 * only, so only for a store with the New Page Builder switch: the Angular one has no such editor.
 */
export function useSharedComponentDesigner() {
  const { openBlade } = useBlade();

  return (storeId: string, sharedComponentId: string, onClose?: () => void) =>
    openBlade({ name: "PageBuilderDesigner", query: { storeId, sharedComponentId }, onClose });
}
