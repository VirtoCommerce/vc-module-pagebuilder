# Creating New Block: Step by Step Instructions

## Intro

This tutorial will show you how to create a new block. We are going to create a simple block that will display selected products on the front end.

## Step 1: Define Block Requirements

Let's say your UI designer created the following mockup:

![block mockup](media/create-new-block/01-block-mockup.png)

## Step 2: Define Property List

Here, we have three property fields:

1. Block title
1. Description text in the rich text format
1. Product list

## Step 3: Create Block Descriptor

Each block has a descriptor file. It is a JSON file that contains all information about the block. The file is stored within the theme at the following path: `/config/schemas/sections/<block-alias>.json`.

Let's give our block a name, say, `demo-product-list`, and create a file, `demo-product-list.json`, with the following content:

```json
{
  "name": "Demo products list",
  "icon": "inventory_2",
  "displayField": "name",
  "settings": [
    {
      "id": "title",
      "label": "Title",
      "type": "string",
      "default": "Title for products"
    },
    {
      "id": "content",
      "label": "Promo text",
      "type": "markdown",
      "resultType": "markdown",
      "default": "Here can be some promo text, or something similar to it\n\nLorem ipsum dolor sit amet, consectetur..."
    }
  ]
}
```

At this point, we just added two fields. We will add other properties later.

## Step 4: Add block to Template Descriptor

There may be many types of templates within a single theme, so we have to specify which templates our block may be used in.

This means we need to add our block to the `page` template descriptor. For that, open the file at `/config/schemas/templates/page.json` and add the block to the `sections` section:

```json
{
  ...
  "sections": [
    ...
    "demo-product-list"
  ]
  ...
}
```

## Step 5: Add Block Layout

Finally, you need to add block layout. The layout is a file that contains HTML markup and logic for the block. Since we use the `vc-theme-b2b-vue` theme based on the Vue.js framework, the layout must also be a Vue component.

Open the theme and create the `/client-app/shared/static-content/components/demo-product-list.vue` file with the following content:
<!--todo: check the layout--->

```vue
<template>
  <div class="pt-6 pb-16">
    <div class="w-full max-w-screen-2xl mx-auto px-5 md:px-12">
      <h2 class="text-2xl">{{ model.title }}</h2>
      <div class="text-lg">
        <VcMarkdownRender :src="model.content" class="text-gray-500"></VcMarkdownRender>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps({
  model: {
    type: Object,
    required: true,
  },
});
</script>
```

Next, register the new block in the theme. Open the `/client-app/shared/static-content/components/index.ts` file and add the following line:

```ts
...
import DemoProductList from "./demo-product-list.vue";

const templateBlocks: { [key: string]: Component } = {
  ...
  "demo-product-list": DemoProductList,
};
...
```

Now, we need to recompile the theme. Open the terminal in the theme folder and run the following command:

```bash
yarn run build
```

## Step 6: Create New Page and Add Block to It

Open the admin panel, go to the current store content, open page list, click ***Add*** on the toolbar, and select ***Design page***:

![Select design page](media/create-new-block/select-design-page.png)

Fill out the page fields and click ***Create*** on the bottom of the screen:

![Add new page](media/create-new-block/create-new-page.png)

Now, we need to add new block to the page. Click the ***Add block*** button on the left bottom part of the page builder and select the ***Demo product list*** block:

![Add block](media/create-new-block/add-block.png)

The block has been added to the page, and we can see it in the preview area:

![Design block](media/create-new-block/design-block.png)

## Step 7: Add Property for Products to Block

Now we need to add products to the block. Open the file with the block settings and add the following item to the `settings` section:

```json
{
  ...
  "settings": [
    ...
    {
      "id": "products",
      "label": "Products",
      "type": "list",
      "default": [],
      "displayField": "product.name",
      "element": [
        {
          "id": "product",
          "label": "Product",
          "type": "select",
          "equalKey": "id",
          "request": {
            "url": "/graphql",
            "method": "post",
            "body": {
              "operationName": null,
              "variables": {},
              "query": "{products(storeId:\"{{location.params.storeId}}\"){items{id,code,name,imgSrc,prices{currency,list{formattedAmount}}}}}"
            },
            "cacheable": true,
            "response": {
              "result": "data.products.items"
            },
            "label": "name"
          }
        }
      ]
    }
  ]
}
```

We now can get products in the page builder:

![Get product](media/create-new-block/select-product.png)

## Last Step: Layout for Product List

The last step to complete the block is the product display in the Vue component. Open the block layout file and add the following code:

```vue
<div class="flex flex-row justify-center space-x-4">
  <div v-for="item in model.products" :key="item.product?.id" :product="item">
    <div v-show="item.product" class="flex flex-col w-48">
      <VcImage
        :src="item.product.imgSrc"
        :alt="item.product.name"
        size-suffix="md"
        class="w-full h-full rounded object-cover object-center select-none space-x-4"
      />
      <div class="flex flex-row space-x-4">
        <div class="grow truncate">{{ item.product.name }}</div>
        <div class="whitespace-nowrap">{{ item.product.prices[0].list.formattedAmount }}</div>
      </div>
    </div>
  </div>
</div>
```
Finaly, here is the result:

![result](media/create-new-block/result.png)
