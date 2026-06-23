---
name: local-pagebuilder-theme-setup
description: How to populate the page-builder block library locally (theme schemas in Content storage)
metadata:
  type: project
---

The page-builder "Add block" library and templates come from **theme schema files in the platform Content (themes) blob storage**, NOT from code. `PageBuilderController` reads `Themes/{storeId}/{theme}/config/schemas/{sections,blocks,objects,shared,templates}/*.json` and `config/settings_schema.json`. Empty folders → empty library + nothing to preview.

**Local fix (this machine, 2026-06):** copy vc-frontend's bundled schemas into the platform Content storage.
- Source: `c:\Projects\git\VirtoCommerce\vc-frontend\client-app\plugins\builder-preview\schemas\` (blocks/sections/objects/shared/templates + settings_schema.json).
- Platform Content root: appsettings `Content.FileSystem.RootPath = ~/cms-content`, `~` = platform `wwwroot`. PathMapping `themes = ["Themes","_storeId"]`.
- Target: `c:\vc-platform-3-demo\platform-net10\wwwroot\cms-content\Themes\B2B-store\default\config\` — put the 5 subfolders under `config\schemas\` and `settings_schema.json` directly under `config\`.
- Store has no `DefaultThemeName` → designer uses theme `default`, so files go under `default\`.

Verify: `GET /api/pagebuilder/sections?storeId=B2B-store&theme=default` returns non-empty sections/blocks. Confirmed library populates (Call to Action, Category, Products, Text, Title, etc.).

**Preview pane** = iframe of vc-frontend at the store `Url` (`https://localhost:3000`) + `/designer-preview?ep=https://localhost:5001`. Storefront↔designer connect via the Vite same-origin proxy (no CORS needed; platform OPTIONS preflight 404s, which is fine). The red 🚫 "localhost refused to connect" means the vc-frontend Vite dev server is down/recompiling — restart `yarn dev`; when warm it renders B2B-store. Related: [[content-stream-sql-portability]].
