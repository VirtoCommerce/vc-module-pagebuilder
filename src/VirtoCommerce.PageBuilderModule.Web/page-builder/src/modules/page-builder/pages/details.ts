import { DynamicDetailsSchema } from "@vc-shell/framework";

export const details: DynamicDetailsSchema = {
  settings: {
    url: "/page-builder-details",
    id: "PageBuilderDetails",
    localizationPrefix: "PAGE_BUILDER",
    composable: "usePageBuilderDetails",
    component: "DynamicBladeForm",
    toolbar: [
      {
        id: "save",
        icon: "fas fa-save",
        title: "PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.SAVE",
        method: "saveChanges",
      },
      {
        id: "delete",
        icon: "fas fa-trash",
        title: "PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.DELETE",
        method: "remove",
      },
      /*
      {
        id: "previewPage",
        icon: "fas fa-eye",
        title: "PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.PREVIEW",
        method: "previewPage",
      },
      */
      {
        id: "openPageDesigner",
        icon: "fas fa-crop",
        title: "PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.DESIGNER",
        method: "openPageDesigner",
      },
      {
        id: "publishPage",
        icon: "fas fa-file",
        title: "PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.PUBLISH",
        method: "publishPage",
      },
      {
        id: "unpublishPage",
        icon: "fas fa-file-alt",
        title: "PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.UNPUBLISH",
        method: "unpublishPage",
      },
    ],
  },
  content: [
    {
      id: "dynamicItemForm",
      component: "vc-form",
      children: [
        {
          id: "name",
          component: "vc-input",
          label: "PAGE_BUILDER.PAGES.DETAILS.FIELDS.NAME",
          rules: { required: true },
          property: "name",
          disabled: { method: "isReadOnly" },
        },
        {
          id: "cultureName",
          component: "vc-select",
          label: "PAGE_BUILDER.PAGES.DETAILS.FIELDS.CULTURE_NAME",
          property: "cultureName",
          optionValue: "name",
          optionLabel: "name",
          optionsMethod: "loadCultureNames",
          disabled: { method: "isReadOnly" },
        },
        {
          id: "permalink",
          component: "vc-input",
          label: "PAGE_BUILDER.PAGES.DETAILS.FIELDS.PERMALINK",
          rules: { required: false },
          property: "permalink",
          disabled: { method: "isReadOnly" },
        },
      ],
    },
  ],
};
