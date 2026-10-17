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
import "./styles.css";

// The AngularJS module's "Page Builder" widget on the store blade ("storeDetail"): the store's pages
registerExternalWidget({
  id: "PageBuilderStore",
  title: "Page Builder",
  component: markRaw(StorePageBuilderWidget),
  targetBlades: ["StoreDetails"],
  // not on a store that isn't saved yet
  isVisible: (blade) => !!blade?.param,
});

// A site page (the Content module's SiteDetails): its pages are the page's content, and the site's assets and shared
// components open from widgets next to Files and Settings
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

// the site's GitHub connection: set up, switched on and off per site
registerExternalWidget({ id: "SiteGit", title: "History", component: SiteGitWidget, targetBlades: ["SiteDetails"] });
// version history is set up once for every site: on the Sites panel
registerExternalWidget({ id: "SitesGitHub", title: "History", component: SitesGitHubWidget, targetBlades: ["ContentStores"] });

export default defineAppModule({
  blades: { PageBuilderPages, PageBuilderPageDetails, PageBuilderDesigner, PageBuilderAssets, PageBuilderSharedComponents, PageBuilderGit, PageBuilderGitHub },
  locales,
});
