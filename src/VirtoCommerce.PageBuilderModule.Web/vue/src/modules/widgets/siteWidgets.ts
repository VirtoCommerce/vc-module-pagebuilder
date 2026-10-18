import { computed, defineComponent, h, markRaw, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { injectBladeContext, useBlade } from "@vc-shell/framework";
import { VcWidget } from "@vc-shell/framework/ui";
import { getAuthHeaders } from "../page-builder/composables/usePageContentApi";
import { gitHubConnectionUrl, type GitHubConnection } from "./siteGit";
import "./siteWidgets.css";

/** A widget on a site page (the Content module's SiteDetails) that opens one of this module's blades for the site. */
function siteWidget(icon: string, title: string, blade: string) {
  return markRaw(
    defineComponent({
      setup() {
        const { t } = useI18n({ useScope: "global" });
        // the store shared by SiteDetails via defineBladeContext({ item })
        const ctx = injectBladeContext();
        const storeId = computed(() => (ctx.value.item as { id?: string } | undefined)?.id);
        const { openBlade } = useBlade();
        return () =>
          h(VcWidget, {
            icon,
            title: t(title),
            disabled: !storeId.value,
            onClick: () => storeId.value && openBlade({ name: blade, param: storeId.value }),
          });
      },
    }),
  );
}

export const SiteAssetsWidget = siteWidget("lucide-images", "ASSET_LIBRARY.MENU.TITLE", "PageBuilderAssets");
export const SiteSharedComponentsWidget = siteWidget("lucide-blocks", "SHARED_COMPONENTS.MENU.TITLE", "PageBuilderSharedComponents");
export const SiteGitWidget = siteWidget("lucide-history", "PAGE_BUILDER.GIT.WIDGET", "PageBuilderGit");

/**
 * On the Sites panel: version history for every site, in words an editor knows rather than "GitHub". "History on", with
 * a green dot, once the platform keeps its sites' versions; "Set up history" until then. Opens the setup and the
 * connection (PageBuilderGitHub), and reads the state again when that closes.
 */
export const SitesGitHubWidget = markRaw(
  defineComponent({
    setup() {
      const { t } = useI18n({ useScope: "global" });
      const { openBlade } = useBlade();
      const on = ref<boolean>();
      async function load() {
        // from the settings only: no GitHub call each time the Sites panel opens
        const response = await fetch(`${gitHubConnectionUrl}?live=false`, { headers: await getAuthHeaders() }).catch(() => undefined);
        const connection = response?.ok ? ((await response.json()) as GitHubConnection) : undefined;
        on.value = !!connection?.configured && !connection.error;
      }
      onMounted(load);
      return () =>
        h("span", { class: "pb-sites-history" }, [
          h(VcWidget, {
            icon: "lucide-history",
            title: t(on.value ? "PAGE_BUILDER.GITHUB.WIDGET_ON" : "PAGE_BUILDER.GITHUB.WIDGET_OFF"),
            onClick: () => openBlade({ name: "PageBuilderGitHub", onClose: load }),
          }),
          on.value ? h("span", { class: "pb-sites-history__dot", "aria-hidden": "true" }) : null,
        ]);
    },
  }),
);
