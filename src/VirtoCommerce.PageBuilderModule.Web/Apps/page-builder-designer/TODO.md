* [x] Operations with blocks
  * [x] Sorting
  * [x] Adding
  * [x] Deleting
  * [x] Updating
  * [x] Cloning block
  * [x] Visibility toggle
* [ ] undo/redo
* [x] copy/paste
* [ ] saving process
  * [x] single template
  * [ ] each template independently
  * [ ] all templates together
* [x] viewing modes
  * [x] desktop
  * [x] mobile
  * [x] tablet
* [ ] preview
  * [ ] in new tab
  * [x] in iframe
* [ ] upload files to storage
* [ ] choose template in template selector
  * [ ] page search in template selector
* [ ] editors
  * [ ] collection
    * [x] inline
    * [ ] panel
    * [ ] popup
  * [ ] object
    * [x] inline
    * [ ] panel
    * [ ] popup
  * [x] load data for select control
  * [ ] search
  * [x] files
  * [x] images
* [x] elements layout
* [x] preferences for page/template
* [x] theme settings
* [ ] platform integration
* [ ] marketplace integration
* [ ] check todo list
* [ ] authorization
* [ ] animation
* [ ] visibility eval-scripts
* [ ] select eval-scripts for options
* [ ] check other evals
* [ ] ref can be a full request
* [ ] custom ref
* [ ] use external objects
* [x] shared settings
    * [x] named settings
* [ ] describe preview message in template settings (and use context to generate it)
* [ ] error in context menu of unexpected block type

* remove from tsconfig.json
  * "resolveJsonModule": true,
  * "allowSyntheticDefaultImports": true,

* theme name is always 'default' in module

## Tailwind v4 migration

Currently on Tailwind **v3.4**. Migration to v4 involves:

* [ ] Run `npx @tailwindcss/upgrade` — official codemod handles most of the routine
* [ ] Replace PostCSS plugin: `tailwindcss` → `@tailwindcss/postcss`, update `.postcssrc.json`
* [ ] Replace CSS directives: `@tailwind base/components/utilities` → `@import 'tailwindcss'`
* [ ] Migrate `tailwind.config.js` to CSS-first config (`@theme { }` block in styles) — config file is no longer needed
* [ ] Update plugins: `@tailwindcss/forms` and `@tailwindcss/typography` have v4-compatible releases, verify import syntax
* [ ] Audit utility class changes: some names changed in v4 (e.g. `shadow-sm` scale, color palette tokens); codemod covers most but check manually
* [ ] Verify `@apply` usage still works as expected after migration

Reference: https://tailwindcss.com/docs/upgrade-guide

## Future / Angular upgrades

* [ ] **Angular 22-23**: migrate dynamic forms from `UntypedFormGroup` + `valueChanges` to Signal Forms
  * Wait until Signal Forms is out of developer preview and supports dynamic schema-driven forms
  * Key files: `forms.helper.ts`, `dynamic-form.component.ts`, `base-control.directive.ts`, `control-holder.component.ts`
  * Also revisit `ControlValueAccessor` in `ngv-datepicker` once signals-compatible API is stable

