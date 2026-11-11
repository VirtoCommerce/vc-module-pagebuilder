import { computed, defineComponent, h, markRaw, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { injectBladeContext, useApiClient, useBlade } from "@vc-shell/framework";
import { VcWidget } from "@vc-shell/framework/ui";
import { GitContentSetupClient } from "../../api_client/virtocommerce.pagebuildermodule";
import "./siteWidgets.css";

const { getApiClient } = useApiClient(GitContentSetupClient);

function siteWidget(icon: string, title: string, blade: string) {
  return markRaw(
    defineComponent({
      setup() {
        const { t } = useI18n({ useScope: "global" });
        // Both host blades share the store as their blade context's item.
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

export const StorePageBuilderWidget = siteWidget("lucide-layout-template", "PAGE_BUILDER.WIDGETS.STORE", "PageBuilderPages");
export const SiteAssetsWidget = siteWidget("lucide-images", "ASSET_LIBRARY.MENU.TITLE", "PageBuilderAssets");
export const SiteSharedComponentsWidget = siteWidget("lucide-blocks", "SHARED_COMPONENTS.MENU.TITLE", "PageBuilderSharedComponents");
export const SiteGitWidget = siteWidget("lucide-history", "PAGE_BUILDER.GIT.WIDGET", "PageBuilderGit");

export const SitesGitHubWidget = markRaw(
  defineComponent({
    setup() {
      const { t } = useI18n({ useScope: "global" });
      const { openBlade } = useBlade();
      const on = ref<boolean>();
      async function load() {
        // live=false reads the settings only, so opening the Sites panel makes no GitHub call.
        const connection = await (await getApiClient()).getConnection(false).catch(() => undefined);
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
