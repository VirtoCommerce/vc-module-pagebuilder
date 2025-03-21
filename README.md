
# Overview

The Page Builder allows creating ecommerce pages from blocks and editing them using a visual editor.

Each page consists of several different blocks. The block view and settings depend on web page requirements.

The page is created in the builder as a list of blocks with specific settings applied to each block. All data are saved in a Json file format.

![image](https://user-images.githubusercontent.com/7639413/195313557-91c00a53-ea2a-4572-aca7-927e210d17b7.png)

## For Business
* Build e-commerce landing pages – simple and faster.
* Look at the content as the end customer see it.
* Mobile-friendliness by design.
* Ready-to-go building blocks and recommendations.

## For Developers
* Integratable into your unique design.
* Configurable set of favourites blocks.
* More blocks. Less code. Create new blocks with flexible JSON schema.
* Can be integrated with your existing sites and apps.
* Ready for integration with DevOps.
* Ready for Publishing process.

## Key Features
* Build a new landing page visually without developer.
* Theme editor.
* Ecommerce page customization (Preview).
* Preview content.
* Block library.
* Seo by design.
* Mobile-friendliness by design
* Block management with Drag and drop, Copy, Paste, Hide, etc.
* Integration with Virto Frontend.
* Native Extensibility Framework.
* Permissions.

## Getting started

### Prerequisites
1. Virto Commerce (`vc-platform`)  [Quick start](https://docs.virtocommerce.org/platform/developer-guide/)
1. Virto Frontend (`vc-frontend`).  [Deploy Frontend](https://docs.virtocommerce.org/storefront/developer-guide/deployment/)
1. Page Builder Module (`vc-module-pagebuilder`). [Download and Install](https://github.com/VirtoCommerce/vc-module-pagebuilder/releases).

### Setup Content
Check that Virto Commerce Platform and Storefront use same Shared Content folder.

### Setup Content Module
Content configuration should be extended with `PathMappings` section.

appsettings.json
```json
    "Content": {
        ...
        "PathMappings":{
            "pages": [
                "Themes",
                "_storeId",
                "_theme",
                "content/pages",
            ],
            "themes": [
                "Themes",
                "_storeId"
            ]
        }
        ...
    },
```

Environment Variables
```yaml
      Content__PathMappings__pages__0: "Themes"
      Content__PathMappings__pages__1: "_storeId"
      Content__PathMappings__pages__2: "_theme"
      Content__PathMappings__pages__3: "content/pages"
      Content__PathMappings__themes__0: "Themes"
      Content__PathMappings__themes__1: "_storeId"
```

### Setup Store
Public Store URL should be configured.

![image](https://user-images.githubusercontent.com/7639413/195312399-ec9a6c1e-e0d2-4798-8c37-1310f9c3a8ea.png)

1. Open Virto Commerce Admin UI.
1. Select `Stores`.
1. Select Current Store.
1. Setup Store URL if it's empty
1. Click Save button to apply.

### Purge Cache
You can purge static page from storefront cache by event. Otherwise, you will need to wait for cache expiration.

Virto Storefront has `/storefrontapi/content/reset-cache` end point for static page invalidation.

We recommend using Webhooks module.

1. Install `vc-module-webhooks` module.
1. Open Virto Commerce Admin UI.
1. Select Webhooks module.
1. Click `Add` button in toolbar to create a new webhook subscription.
1. Enter webhook subscription name.
1. Select `Page Builder Content Changed Event` event in Events drop-down.
1. Select `Path` and `Type` in additional fields.
1. Turn on `Activate` checkbox.
1. Enter Storefront end point in URL. Ex: https://www.mypublic-domain.com//storefrontapi/content/reset-cache
1. Save webhook subscription.

**!!note: change image to correct (event name was changed)**

![image](docs/media/screen-webhook-settings.png)

### Run
1. Open Virto Commerce Admin UI.
1. Select Content module.
1. Find Store and Select Pages widget.
1. Click `Add` button in toolbar.
1. Select 'Design page'.
1. Provide page file name, public name and permalink. Ex: 'black-friday-2022', 'Black Friday 2022' and `blackfriday-2022`.
1. Click `Create` button

Now, you should be redirected to page builder application and you can create a first page.

Later, you can open and edit page builder pages, directly from content module.

## Page Builder Theme Structure
Page Builder reads meta-data from theme. Developers can extend or customize page builder behaviour by theme repository.

```text
├── client-app                    // The main folder for the application.
|   ├── shared                    // Assets needed to be precompiled during building.
|   |   └── static-content
|   |      └── components         // Vue Components for rendering Page Builder elements.
├── config
|   |   └── schemas               // All Page Builder meta information are stored here.
|   |      ├── blocks             // Block definitions.
|   |      |   └──...
|   |      ├── objects            // Object definitions.
|   |      |   └──...
|   |      ├── sections           // Sections definitions.
|   |      |   └──...
|   |      ├── shared             // Global settings folder.
|   |      |   └── _blocks.json   // Global settings for blocks.
|   |      |   └── _sections.json // Global settings for sections.
|   |      └── templates
|   |          └── page.json      // Page Builder configuration for static pages.
|   └── settings_schema.json      // Theme settings schema file. Page Builder uses it for Theme and Preset editor.
|   └── settings_data.json        // Theme config file.
```

## Notes

To enable the preview of your site within the Page Builder application, the site must allow itself to be displayed in an iframe. This can be achieved by setting the appropriate headers in your server configuration.

Add the following headers to your server configuration:

```http
Content-Security-Policy: "frame-ancestors 'self' https://localhost:5001;"
Cross-Origin-Resource-Policy: "cross-origin"
Cross-Origin-Embedder-Policy: "credentialless"
```

These headers ensure that the site can be embedded in an iframe and handle cross-origin requests appropriately.

## Documentation

* [PageBuidler module user documentation](https://docs.virtocommerce.org/platform/user-guide/page-builder/overview/)
* [REST API](https://virtostart-demo-admin.govirto.com/docs/index.html?urls.primaryName=VirtoCommerce.PageBuilderModule)
* [View on GitHub](https://github.com/VirtoCommerce/vc-module-pagebuilder)

## References

* [Deployment](https://docs.virtocommerce.org/platform/developer-guide/Tutorials-and-How-tos/Tutorials/deploy-module-from-source-code/)
* [Installation](https://docs.virtocommerce.org/platform/user-guide/modules-installation/)
* [Home](https://virtocommerce.com)
* [Community](https://www.virtocommerce.org)
* [Download latest release](https://github.com/VirtoCommerce/vc-module-pagebuilder/releases/latest)

## License
Copyright (c) Virto Solutions LTD. All rights reserved.

Licensed under the Virto Commerce Open Software License (the "License"); you may not use this file except in compliance with the License. You may obtain a copy of the License at

http://virtocommerce.com/opensourcelicense

Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
