export interface PageDesignerContext {
  groupId?: string | null;
  storeId?: string | null;
  cultureName?: string | null;
  status?: string | null;
}

const appPath = "/apps/page-builder-shell/";
const classicDesignerPath = "/Modules/$(VirtoCommerce.PageBuilderModule)/Content/page-builder-designer/index.html";

export function canOpenPageDesigner(
  context: PageDesignerContext,
): context is PageDesignerContext & { groupId: string; storeId: string } {
  return Boolean(context.groupId?.trim() && context.storeId?.trim() && context.status !== "Archived");
}

export function buildPageDesignerUrl(context: PageDesignerContext, platformUrl: string): string {
  if (!canOpenPageDesigner(context)) {
    throw new Error("Can't open page.");
  }

  const parameters = new URLSearchParams({ storeId: context.storeId.trim(), designGroup: context.groupId.trim() });
  const normalizedPlatformUrl = platformUrl.replace(/\/+$/, "");

  return `${normalizedPlatformUrl}${appPath}?${parameters.toString()}`;
}

export function buildClassicDesignerUrl(context: PageDesignerContext, platformUrl: string): string {
  if (!canOpenPageDesigner(context)) {
    throw new Error("Can't open page.");
  }

  const routeParameters = new URLSearchParams({ type: "pages", groupId: context.groupId.trim() });
  if (context.cultureName) {
    routeParameters.set("cultureName", context.cultureName);
  }
  const storeId = encodeURIComponent(context.storeId.trim());

  return `${platformUrl.replace(/\/+$/, "")}${classicDesignerPath}?storeId=${storeId}#/pages?${routeParameters.toString()}`;
}

export function openPageDesigner(context: PageDesignerContext, classic = false): void {
  const platformUrl = (import.meta.env.DEV && import.meta.env.APP_PLATFORM_URL) || window.location.origin;
  const url = classic ? buildClassicDesignerUrl(context, platformUrl) : buildPageDesignerUrl(context, platformUrl);

  window.open(url, "_blank", "noopener,noreferrer");
}
