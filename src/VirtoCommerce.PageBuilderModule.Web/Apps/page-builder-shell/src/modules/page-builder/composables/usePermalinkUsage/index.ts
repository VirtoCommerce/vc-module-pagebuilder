import { useI18n } from "vue-i18n";
import { useApiClient, usePopup } from "@vc-shell/framework";
import { PageBuilderPageClient, PermalinkUsageRequest } from "../../../../api_client/virtocommerce.pagebuildermodule";

const { getApiClient } = useApiClient(PageBuilderPageClient);

/**
 * Two active pages with the same permalink and language make the storefront serve only one of them,
 * so saving such a page asks for confirmation instead of failing.
 */
export function usePermalinkUsage() {
  const { t } = useI18n({ useScope: "global" });
  const { showConfirmation } = usePopup();

  /** @returns whether to go on with the save. */
  async function confirmPermalinkUsage(request: PermalinkUsageRequest): Promise<boolean> {
    if (!request.storeId || !request.permalink) {
      return true;
    }

    const apiClient = await getApiClient();
    const usage = await apiClient.findPermalinkUsage(request);
    if (!usage.length) {
      return true;
    }

    const pages = usage.map((x) => `${x.name} (${x.cultureName})`).join(", ");
    return showConfirmation(t("PAGE_BUILDER.PAGES.ALERTS.PERMALINK_IN_USE", { permalink: request.permalink, pages }));
  }

  return { confirmPermalinkUsage };
}
