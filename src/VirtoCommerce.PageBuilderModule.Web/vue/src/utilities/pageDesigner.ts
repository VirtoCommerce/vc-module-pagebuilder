export interface PageDesignerContext {
  groupId?: string | null;
  storeId?: string | null;
  cultureName?: string | null;
  status?: string | null;
}

// the Page Builder app (it opens the designer from ?storeId=…&designGroup=…)
const appPath = "/apps/page-builder-shell/";

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

export function openPageDesigner(context: PageDesignerContext): void {
  const platformUrl = (import.meta.env.DEV && import.meta.env.APP_PLATFORM_URL) || window.location.origin;

  window.open(buildPageDesignerUrl(context, platformUrl), "_blank", "noopener,noreferrer");
}
