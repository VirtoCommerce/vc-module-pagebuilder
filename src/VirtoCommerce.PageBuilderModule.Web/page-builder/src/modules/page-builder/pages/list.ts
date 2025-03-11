import { DynamicGridSchema } from "@vc-shell/framework";

export const grid: DynamicGridSchema = {
  settings: {
    url: "/page-builder",
    id: "PageBuilderList",
    titleTemplate: "Page Builder list",
    localizationPrefix: "PAGE_BUILDER",
    isWorkspace: true,
    composable: "usePageBuilderList",
    component: "DynamicBladeList",
    toolbar: [
      {
        id: "refresh",
        icon: "fas fa-sync-alt",
        title: "Refresh",
        method: "refresh",
      },
    ],
    menuItem: {
      title: "PAGE_BUILDER.MENU.TITLE",
      icon: "fas fa-file-alt",
      priority: 1,
    },
  },
  content: [
    {
      id: "itemsGrid",
      component: "vc-table",
      columns: [
        // You can add columns here
      ],
    },
  ],
};
