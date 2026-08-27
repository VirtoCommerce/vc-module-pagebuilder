# Text Control Descriptor

This control provides a rich-text editor based on [ckeditor-angular](https://www.npmjs.com/package/ckeditor4-angular). It allows users to format text using a WYSIWYG interface.

| Property   | Type   | Description                           |
| ---------- | ------ | ------------------------------------- |
| `config`   | object | [Configuration options](https://ckeditor.com/docs/ckeditor4/latest/api/CKEDITOR_config.html) for the editor. |

## Example

```json
...
    "settings": [
        {
            "id": "caption",
            "type": "text",
            "label": "Caption",
            "default": "Lorem ipsum"
        },
        ...
    ]
...
```

## Page-wide anchor links

The editor's **Link** dialog offers **Link to anchor in the text**. Out of the box CKEditor only
lists anchors found in the field the dialog was opened from, which makes a table of contents or a
"back to top" link impossible to build — the targets live in other sections.

Page Builder replaces that list with every anchor on the page being edited:

* the [`anchor` setting](../schemas.md#the-anchor-setting) of each section and block, falling back to
  the generated item id;
* in-text anchors added with the **Anchor** toolbar button (`<a name="...">`);
* element ids authored through the **Source** view.

Each entry is listed as `<item name> (<anchor>)` — the name comes from the schema's `displayField`,
falling back to the schema `name`. Hidden sections and blocks are left out, because the storefront
does not render them. The link is stored as a plain `#anchor` href, and reopening it resolves against
the same list — so an anchor in another section is found instead of reported as missing. An anchor
that is no longer on the page stays selected rather than silently clearing the link.

Since every anchor is offered through the single **By Anchor Name** list, the **By Element Id** list
is hidden and the picker uses the full width of the dialog.

The **Anchor** panel of a section or block shows the value to link to, with a copy button.

!!! note
    The **Link to anchor in the text** option itself cannot be removed through configuration. It is
    part of the link plugin's dialog definition, so neither `removePlugins: "anchor"` nor dropping
    `Anchor` from the toolbar hides it — those only affect the toolbar button that creates in-text
    anchors.


<br>
<br>
********

<div style="display: flex; justify-content: space-between;">
    <a href="../string">← String </a>
    <a href="../../Payments/new-payment-method-registration">Payments. New payment method →</a>
</div>