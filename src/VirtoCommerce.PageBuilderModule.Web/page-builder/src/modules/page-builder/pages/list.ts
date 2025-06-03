import { DynamicGridSchema } from "@vc-shell/framework";

export const grid: DynamicGridSchema = {
  settings: {
    url: "/page-builder",
    id: "PageBuilderList",
    titleTemplate: "Pages list",
    localizationPrefix: "PAGE_BUILDER",
    isWorkspace: true,
    composable: "usePageBuilderList",
    component: "DynamicBladeList",
    toolbar: [
      {
        id: "add",
        icon: "material-add",
        title: "PAGE_BUILDER.PAGES.LIST.TOOLBAR.ADD",
        method: "openAddBlade",
      },
      {
        id: "refresh",
        icon: "material-refresh",
        title: "PAGE_BUILDER.PAGES.LIST.TOOLBAR.REFRESH",
        method: "refresh",
      },
      {
        id: "remove",
        icon: "material-delete",
        title: "PAGE_BUILDER.PAGES.LIST.TOOLBAR.REMOVE",
        method: "removeItems",
      },
    ],
    menuItem: {
      title: "PAGE_BUILDER.MENU.TITLE",
      icon: "material-article",
      priority: 1,
    },
  },
  content: [
    {
      id: "itemsGrid",
      component: "vc-table",      
      multiselect: true,
      filter: {
        columns: [
          {
            id: "pageStatusFilter",
            title: "PAGE_BUILDER.PAGES.LIST.TABLE.FILTER.STATUS",
            controls: [
              {
                id: "pageStatusCheckbox",
                field: "status",
                component: "vc-checkbox",
                multiple: false,
                data: "pageStatuses",
                optionValue: "value",
                optionLabel: "label",
              },
            ],
          },
        ],
      },
      actions: [
        {
          id: "delete",
          icon: "material-delete",
          title: "PAGE_BUILDER.PAGES.LIST.TABLE.ACTIONS.DELETE",
          method: "removeItems",
          type: "danger",
        },
      ],
      columns: [
        {
          id: "name",
          title: "PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.NAME",
          alwaysVisible: true,
          sortable: true,
        },
        {
          id: "cultureName",
          title: "PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.CULTURE_NAME",
          alwaysVisible: true,
          sortable: true,
        },
        {
          id: "permalink",
          title: "PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.PERMALINK",
          alwaysVisible: true,
        },
        {
          id: "modifiedDate",
          title: "PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.MODIFIED_DATE",
          type: "date-time",
          alwaysVisible: true,
          sortable: true,
        },
        {
          id: "modifiedBy",
          title: "PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.MODIFIED_BY",
        },
        {
          id: "status",
          title: "PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.STATUS",
          customTemplate: {
            component: "PageStatus",
          },
        },
      ],
    },
  ],
};
