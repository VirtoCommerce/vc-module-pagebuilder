import { getBladeConfig, useBlade } from "@vc-shell/framework";
import { canOpenPageDesigner, openPageDesigner, type PageDesignerContext } from "../../../../utilities/pageDesigner";

/**
 * Opens a page in the designer: in the Admin Shell as the full-screen PageBuilderDesigner blade (param = page group id),
 * in the standalone app, which doesn't register that blade, in a new tab as before.
 */
export function usePageDesigner() {
  const { openBlade } = useBlade();

  return (context: PageDesignerContext, onClose?: () => void) => {
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

/** Opens a shared component in the designer: its sections, edited once for every page that places it. */
export function useSharedComponentDesigner() {
  const { openBlade } = useBlade();

  return (storeId: string, sharedComponentId: string, onClose?: () => void) =>
    openBlade({ name: "PageBuilderDesigner", query: { storeId, sharedComponentId }, onClose });
}
