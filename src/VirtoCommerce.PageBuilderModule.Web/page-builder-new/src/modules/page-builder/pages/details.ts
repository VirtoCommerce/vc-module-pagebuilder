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
        icon: "material-save",
        title: "PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.SAVE",
        method: "saveChanges",
      },
      {
        id: "delete",
        icon: "material-delete",
        title: "PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.DELETE",
        method: "remove",
      },
      /*
      {
        id: "previewPage",
        icon: "material-visibility",
        title: "PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.PREVIEW",
        method: "previewPage",
      },
      */
      {
        id: "openPageDesigner",
        icon: "material-crop",
        title: "PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.DESIGNER",
        method: "openPageDesigner",
      },
      {
        id: "publishPage",
        icon: "material-description",
        title: "PAGE_BUILDER.PAGES.DETAILS.TOOLBAR.PUBLISH",
        method: "publishPage",
      },
      {
        id: "unpublishPage",
        icon: "material-article",
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
          id: "status",
          component: "vc-custom",
          name: "PageStatus",
        },
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
        {
          id: "visibility",
          component: "vc-switch",
          label: "PAGE_BUILDER.PAGES.DETAILS.FIELDS.VISIBILITY",
          property: "visibility",
          tooltip: "PAGE_BUILDER.PAGES.DETAILS.TOOLTIPS.VISIBILITY",
          disabled: { method: "isReadOnly" },
        },
        {
          id: "userGroups",
          component: "vc-select",
          label: "PAGE_BUILDER.PAGES.DETAILS.FIELDS.USER_GROUPS",
          property: "userGroups",
          optionValue: "name",
          optionLabel: "name",
          searchable: true,
          optionsMethod: "loadUserGroups",
          multiple: true,
          disabled: { method: "isReadOnly" },
        },
        {
          id: "startDate",
          component: "vc-input",
          variant: "datetime-local",
          label: "PAGE_BUILDER.PAGES.DETAILS.FIELDS.START_DATE",
          rules: { required: false },
          disabled: { method: "isReadOnly" },
          property: "startDate",
        },
        {
          id: "endDate",
          component: "vc-input",
          variant: "datetime-local",
          label: "PAGE_BUILDER.PAGES.DETAILS.FIELDS.END_DATE",
          rules: { required: false },
          disabled: { method: "isReadOnly" },
          property: "endDate",
        },
      ],
    },
  ],
};
