import { useI18n } from "vue-i18n";
import { useApiClient, usePopup } from "@vc-shell/framework";
import { PageBuilderPageClient, PermalinkUsageRequest } from "../../../../api_client/virtocommerce.pagebuildermodule";

const { getApiClient } = useApiClient(PageBuilderPageClient);

/** Two active pages with the same permalink and language make the storefront serve only one, so a save asks for confirmation. */
export function usePermalinkUsage() {
  const { t } = useI18n({ useScope: "global" });
  const { showConfirmation } = usePopup();

  async function confirmPermalinkUsage(request: PermalinkUsageRequest): Promise<boolean> {
    if (!request.storeId || !request.permalink) {
      return true;
    }

    let usage;
    try {
      const apiClient = await getApiClient();
      usage = await apiClient.findPermalinkUsage(request);
    } catch (error) {
      // The check only warns: a failed check must not block saving the page.
      console.warn("Permalink usage check failed", error);
      return true;
    }

    if (!usage.length) {
      return true;
    }

    const pages = usage.map((x) => `${x.name} (${x.cultureName})`).join(", ");
    return showConfirmation(t("PAGE_BUILDER.PAGES.ALERTS.PERMALINK_IN_USE", { permalink: request.permalink, pages }));
  }

  return { confirmPermalinkUsage };
}
