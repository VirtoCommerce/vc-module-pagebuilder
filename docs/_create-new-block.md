# Create new block. Step by step instructions.

## Intro

This tutorial will show you how to create a new block. Let's create a simple block that will display a selected products on the frontend.

## Step 1: Define block requirements

Let's say UI-designer draw the following mockup:

![block mockup](media/create-new-block/01-block-mockup.png)

## Step 2: Define a list of properties

Here we have three property fields:
1. Title for block
1. Some description text, that can be rich-text.
1. Products list.

## Step 3: Create block descriptor

Each block has a descriptor file. It is a JSON file that contains all the information about the block. The file stored in the theme by path `config/schemas/sections/<block-alias>.json`.

Let's name block `demo-product-list` and create file `demo-product-list.json` with the following content;

```json
{
  "name": "Demo products list",
  "icon": "inventory_2",
  "displayField": "name",
  "settings": [
    {
      "id": "title",
      "label": "Title",
      "type": "string"
    },
    {
      "id": "content",
      "label": "Promo text",
      "type": "markdown"
    }
  ]
}
```

# TODO:

* describe all the properties of the block.
* add block to template
* add layout for block
* screen for properties panel
* screen for preview panel
* add properties for preview
* add products property to schema
* add layout for products



