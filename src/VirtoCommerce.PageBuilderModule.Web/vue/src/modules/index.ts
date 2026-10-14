import * as locales from "./locales";
import { defineAppModule, registerExternalWidget } from "@vc-shell/framework";
import { markRaw } from "vue";
import StorePageBuilderWidget from "./widgets/StorePageBuilderWidget.vue";

// The AngularJS module's "Page Builder" widget on the store blade ("storeDetail"): opens the Page Builder app for the store
registerExternalWidget({
  id: "PageBuilderStore",
  title: "Page Builder",
  component: markRaw(StorePageBuilderWidget),
  targetBlades: ["StoreDetails"],
  // not on a store that isn't saved yet
  isVisible: (blade) => !!blade?.param,
});

export default defineAppModule({
  blades: {},
  locales,
});
