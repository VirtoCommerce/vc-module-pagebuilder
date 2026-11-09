import locales from "./locales";
import { defineAppModule, registerExternalWidget } from "@vc-shell/framework";
import { markRaw } from "vue";
import StorePageBuilderWidget from "./widgets/StorePageBuilderWidget.vue";
import SitePagesTab from "./widgets/SitePagesTab.vue";
import { SiteAssetsWidget, SiteGitWidget, SiteSharedComponentsWidget, SitesGitHubWidget } from "./widgets/siteWidgets";
import PageBuilderPages from "./pages/PageBuilderPages.vue";
import PageBuilderAssets from "./pages/PageBuilderAssets.vue";
import PageBuilderSharedComponents from "./pages/PageBuilderSharedComponents.vue";
import PageBuilderDesigner from "./pages/PageBuilderDesigner.vue";
import PageBuilderGit from "./pages/PageBuilderGit.vue";
import PageBuilderGitHub from "./pages/PageBuilderGitHub.vue";
import PageBuilderPageDetails from "./page-builder/pages/PageBuilderPageDetails.vue";
import MultilanguagePageDetails from "./page-builder/pages/MultilanguagePageDetails.vue";
import { definePermalinkRule } from "./page-builder/permalinkRule";
import "./styles.css";

definePermalinkRule();

registerExternalWidget({
  id: "PageBuilderStore",
  title: "Page Builder",
  component: markRaw(StorePageBuilderWidget),
  targetBlades: ["StoreDetails"],
  isVisible: (blade) => !!blade?.param,
});

registerExternalWidget({
  id: "site-content:pages",
  title: "PAGE_BUILDER.SITE_TAB.TITLE",
  component: markRaw(SitePagesTab),
  targetBlades: ["SiteDetails:content"],
});
registerExternalWidget({ id: "SiteAssets", title: "Assets Library", component: SiteAssetsWidget, targetBlades: ["SiteDetails"] });
registerExternalWidget({
  id: "SiteSharedComponents",
  title: "Shared Components",
  component: SiteSharedComponentsWidget,
  targetBlades: ["SiteDetails"],
});

registerExternalWidget({ id: "SiteGit", title: "History", component: SiteGitWidget, targetBlades: ["SiteDetails"] });
registerExternalWidget({ id: "SitesGitHub", title: "History", component: SitesGitHubWidget, targetBlades: ["ContentStores"] });

export default defineAppModule({
  blades: {
    PageBuilderPages,
    PageBuilderPageDetails,
    MultilanguagePageDetails,
    PageBuilderDesigner,
    PageBuilderAssets,
    PageBuilderSharedComponents,
    PageBuilderGit,
    PageBuilderGitHub,
  },
  locales,
});
