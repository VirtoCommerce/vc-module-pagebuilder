export interface PageDesignerContext {
  groupId?: string | null;
  storeId?: string | null;
  cultureName?: string | null;
  status?: string | null;
}

const designerPath = "/Modules/$(VirtoCommerce.PageBuilderModule)/Content/page-builder-designer/index.html";

export function canOpenPageDesigner(
  context: PageDesignerContext,
): context is PageDesignerContext & { groupId: string; storeId: string } {
  return Boolean(context.groupId && context.storeId && context.status !== "Archived");
}

export function buildPageDesignerUrl(context: PageDesignerContext, platformUrl: string): string {
  if (!canOpenPageDesigner(context)) {
    throw new Error("Can't open page.");
  }

  const routeParameters = new URLSearchParams({
    type: "pages",
    groupId: context.groupId,
  });

  if (context.cultureName) {
    routeParameters.set("cultureName", context.cultureName);
  }

  const normalizedPlatformUrl = platformUrl.replace(/\/+$/, "");
  const storeId = encodeURIComponent(context.storeId);

  return `${normalizedPlatformUrl}${designerPath}?storeId=${storeId}#/pages?${routeParameters.toString()}`;
}

export function openPageDesigner(context: PageDesignerContext): void {
  const platformUrl = (import.meta.env.DEV && import.meta.env.APP_PLATFORM_URL) || window.location.origin;

  window.open(buildPageDesignerUrl(context, platformUrl), "_blank");
}
