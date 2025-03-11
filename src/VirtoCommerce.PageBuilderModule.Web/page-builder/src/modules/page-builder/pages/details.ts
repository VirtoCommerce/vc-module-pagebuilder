import { DynamicDetailsSchema } from "@vc-shell/framework";

export const details: DynamicDetailsSchema = {
  settings: {
    url: "/page-builder-details",
    id: "PageBuilderDetails",
    localizationPrefix: "PAGE_BUILDER",
    composable: "usePageBuilderDetails",
    component: "DynamicBladeForm",
  },
  content: [
    {
      id: "dynamicItemForm",
      component: "vc-form",
      children: [
        // You can add fields here
      ],
    },
  ],
};
