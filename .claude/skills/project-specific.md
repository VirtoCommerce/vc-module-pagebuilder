# Project-Specific Knowledge

## Description
Domain logic, platform integration, API patterns, and deployment context for the VirtoCommerce Page Builder module. Use this skill when working with platform communication, API integration, template/section editing logic, Vue shell, AngularJS admin, or .NET backend.

## Triggers
- Working with API calls, HTTP client, config-driven endpoints
- Platform integration (postMessage, BroadcastChannel)
- Template/section/block CRUD operations
- Auth/token management
- C# services, controllers, EF Core migrations
- Vue 3 shell code
- AngularJS admin blades
- Understanding business domain (pages, templates, sections, themes)
- Build/deployment questions

---

## Project Purpose

A **VirtoCommerce platform module** that provides page builder and theme editor functionality. The system allows merchants to:
- Edit page templates by adding/removing/reordering sections and blocks
- Configure section properties via schema-driven dynamic forms
- Preview changes in a live storefront iframe
- Manage theme settings (presets, colors, typography)
- Publish page changes to the storefront

## Module Versions

- Module version: **3.1001.0** (in `Directory.Build.props`)
- .NET: **10.0**
- Platform dependency: **VirtoCommerce 3.1000.0+**

## Domain Model

### Core Entities
- **PageBuilderPage** — A single page with path, template type, group ID, content
- **GroupedPageBuilderPage** — A grouped page variant (for multi-language / multi-variant pages)
- **TemplateEntry** — Metadata about a template (path, type, groupId, state)
- **TemplateModel** — Full template content: `settings` object + `sections` array
- **SectionModel** — A section within a template: `id`, `type`, properties, optional `blocks` array
- **SectionSchema** — Describes a section's editable properties and available blocks
- **BaseControlDescriptor** — Schema for a single form control (id, type, label, default, visibility, tab, group)

### Template Key
Templates identified by computed key: `type::groupId` or `type::path` (from URL query params).

### Control Types
Dynamic form: `text`, `textarea`, `checkbox`, `select`, `color`, `range`, `image`, `images`, `files`, `markdown`, `richtext`, `calendar`, `object`, `array`, and more. Each maps to a component in `core/controls/`.

## C# Backend Architecture

### Project Dependencies
```
Core ← Data ← Web
Core ← Data.SqlServer / Data.MySql / Data.PostgreSql
```

### Key Services (Core interfaces → Data implementations)
- `IPageBuilderPageService` — CRUD for pages
- `IGroupedPageService` — Grouped page management
- `IPagesMigrationService` — Migration logic between formats
- `IContentStreamProvider` / `ContentStreamRepository` — Large content streaming

### API Controllers
- `PageBuilderController` — Main API: templates, sections, schemas, config
- `PageBuilderPageController` — Page CRUD, search, publish

### Events
Event-driven side effects via platform event bus:
- `PageBuilderPageChanging` / `PageBuilderPageChanged`
- `GroupedPageChanging` / `GroupedPageBuilderPageChanged`

Handlers in `Data/Handlers/` react to page save/delete to sync content.

### EF Core
- `PageBuilderModuleDbContext` in Data project
- Migrations in provider projects (`Data.SqlServer`, `Data.MySql`, `Data.PostgreSql`)
- When adding a DB column: add to entity, add to mapping, run `dotnet ef migrations add` in each provider project

### Authorization
- `PageBuilderAuthorizationHandler` validates `PageBuilderAuthorizationRequirement`
- Permissions defined in `ModuleConstants.Security`

## Page designer (Vue)

The page designer is the full-screen `PageBuilderDesigner` blade (`vue/src/modules/pages/PageBuilderDesigner.vue`); its state, schema, document and preview code live in `vue/src/modules/designer/`. It edits database pages (param = page group id) and pages kept as files (query: storeId, path, type), with the git version history when the store keeps its pages in git. The preview is the storefront's `/designer-preview` page in an iframe, driven over postMessage (`designer/preview.ts`). The standalone app opens it from `/apps/page-builder-shell/?storeId=…&designGroup=…` or `&designPath=…&designType=…`, which is what the classic admin's page blade links to.

## Vue 3 Shell App

**Location:** `Apps/page-builder-shell/`
**Framework:** Vue 3 Composition API, Vite 6, TypeScript
**Key libs:** `@vc-shell/framework@1.2.2`, `vue-router`, `vee-validate`, `@vueuse/core`

**Build commands:**
```bash
yarn serve           # Dev
yarn build           # Production
yarn build:dev       # Dev build
yarn type-check      # Vue-tsc
```

**API client** auto-generated via `@vc-shell/api-client-generator`. Regenerate when C# DTOs change.

## AngularJS Admin Integration

**Location:** `Web/Scripts/`
**Module:** `virtoCommerce.pageBuilderModule`
**Purpose:** Integrates with the VirtoCommerce admin portal's legacy blade system

**Components:**
- `blades/pages/edit-page.js` — Page editing blade (427-line controller, known SonarQube issues)
- `widgets/pageBuilder-app-widget.js` — Widget shown in Store detail view
- `resources/pageBuilderApi.js` — `$resource` wrapper for the backend API
- `services/page-builder-file.handler.js` — File upload handler

**Rules:**
- This is **legacy code** — do not add new features
- The known SonarQube issues (long functions, `==` instead of `===`, etc.) are documented in `sonar-inspect.md`
- File is registered and loaded by the VC platform admin shell

## Build & Deployment

- CI: `.github/workflows/module-ci.yml` (Node 20, builds + packages as NuGet)

## Known Issues / sonar-inspect.md

The file `sonar-inspect.md` at repo root documents SonarQube findings that are intentional or low-priority. Before addressing a SonarQube warning, check if it's already documented there.
