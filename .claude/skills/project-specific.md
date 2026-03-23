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

## Angular 20 Designer App

### Platform Integration Architecture

**Iframe Embedding:** The Angular SPA runs inside a `<iframe>` in the VC platform admin.

**Communication channels:**
1. `postMessage` — bidirectional with parent window and preview iframe
2. `BroadcastChannel` (`vc-module-content-channel`) — with VC platform shell

**EventsBusService** routes messages by `target` field:
- `target: 'preview'` → preview iframe via `postMessage`
- `target: 'platform'` → platform via `BroadcastPlatformService`

### Auth Flow
- JWT read from `ls.authenticationData` in localStorage (set by VC platform shell)
- `RefreshTokenInterceptor` attaches `Authorization: Bearer` to all API requests
- Silent token refresh via `/connect/token` before expiry, requests queued during refresh

### Config-Driven HTTP

**Config loading (`AppInitializator`):**
1. Load `data/settings.json` (or `?configUrl=` query param)
2. Optionally load overrides from `/api/pagebuilder/settings`
3. Recursively resolve nested config
4. Store in `AppConfig` singleton

**Config interpolation:** Values support `{{token}}` syntax:
- `location.*` — current URL parts
- `config.*` — other config values
- `settings.*` — loaded settings

**BuilderHttpClient** evaluates `ServerRequestDescriptor`:
```typescript
interface ServerRequestDescriptor {
    url: string;           // with {{token}} placeholders
    method?: string;
    body?: any;
    resultPath?: string;   // jsonpath to extract from response
    headers?: object;
}
```
Features: LRU cache (100 items) for GET, fallback chains, jsonpath response mapping.

### Key Config Keys
| Key | Purpose |
|---|---|
| `templatesListUrl` | List templates |
| `templateUrl` | Load single template |
| `sectionsListUrl` | List available sections |
| `saveTemplates` | Save template changes |
| `settingsDataRequest` | Load theme settings data |
| `settingsSchemaRequest` | Load theme settings schema |
| `saveSettings` | Save theme settings |
| `uploadAssetsRequest` | Upload images/files |
| `publish` / `publishPages` | Publish changes |
| `externalPreview` | External preview URL |

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

- Build output: `dist/template-builder/` — deployed as part of the .NET module
- Budget: 1500KB initial warning, 2MB error
- CI: `.github/workflows/module-ci.yml` (Node 20, builds + packages as NuGet)
- The Angular app lives inside the C# Web project at `Apps/page-builder-designer/`
- `dist/template-builder/` gets bundled into the NuGet package and served by ASP.NET Core static files

## Known Issues / sonar-inspect.md

The file `sonar-inspect.md` at repo root documents SonarQube findings that are intentional or low-priority. Before addressing a SonarQube warning, check if it's already documented there.
