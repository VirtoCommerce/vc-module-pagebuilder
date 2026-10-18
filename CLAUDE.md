# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Overview

A **VirtoCommerce platform module** for page builder / theme editor functionality. The repo contains multiple sub-projects across different stacks:

| Sub-project | Stack | Location |
|---|---|---|
| C# backend | .NET 10, EF Core | `src/VirtoCommerce.PageBuilderModule.*` |
| Vue 3 UI (Admin Shell + standalone shell) | Vue 3, Vite, @vc-shell | `src/.../vue/` |
| AngularJS admin | AngularJS (legacy) | `src/.../Web/Scripts/` |

---

## Commands

### C# / .NET Backend

```bash
# Build solution
dotnet build VirtoCommerce.PageBuilderModule.sln

# Run tests
dotnet test

# Build specific project
dotnet build src/VirtoCommerce.PageBuilderModule.Web/
```

### Vue 3 Shell

All commands run from: `src/VirtoCommerce.PageBuilderModule.Web/vue/` (one project for the Admin Shell UI and the standalone app)

```bash
yarn build                   # Admin Shell UI (Module Federation remote) -> plugins/admin-shell/
yarn build:standalone        # Standalone app -> Apps/page-builder-shell/dist (vite.standalone.config.mts)
yarn type-check              # Vue-tsc type checking (both)
yarn test                    # node:test unit tests (tests/)
```

`Apps/page-builder-shell/` keeps only a package.json whose `build` runs `build:standalone` here, for vc-build's app build.

---

## Architecture

### C# Module Structure

Standard VirtoCommerce module pattern:

- **`Core/`** — Domain models, service interfaces, events, `ModuleConstants`. No dependencies on Data or Web.
- **`Data/`** — EF Core DbContext, entity models, service implementations, authorization handlers, search indexing. Depends on Core and platform.
- **`Data.SqlServer/`, `Data.MySql/`, `Data.PostgreSql/`** — Database provider projects. Each contains EF Core migrations for its target DB.
- **`Web/`** — .NET Web SDK project (output is a library, not executable). Contains API controllers, AngularJS scripts, and serves the compiled Vue apps. Depends on Core + Data.
- **`Tests/`** — xUnit v3 tests.

**Version:** 3.1001.0 (defined in `Directory.Build.props`). Platform dependency: VirtoCommerce 3.1000.0+.

**Key C# patterns:**
- `TreatWarningsAsErrors: true` — no suppressed warnings
- Event-driven architecture: `PageBuilderPageChanging/Changed`, `GroupedPageChanging/Changed`
- Content streaming via `ContentStreamRepository` / `IContentStreamProvider`
- Authorization via `PageBuilderAuthorizationHandler` + `PageBuilderAuthorizationRequirement`

### Vue 3 Shell App

Lives in `vue/`: shared code in `vue/src/` (api_client, utilities, locales, the page-builder / asset-library / shared-components modules), the Admin Shell entry in `vue/src/modules/index.ts` (blades PageBuilderPages, PageBuilderPageDetails, PageBuilderDesigner, PageBuilderAssets, PageBuilderSharedComponents; the StoreDetails widget and the Sites "Pages" tab), the standalone app (served at `/apps/page-builder-shell/`) in `vue/standalone/`. Built with Vite 6 and uses the `@vc-shell/framework` for VirtoCommerce module shell integration. Uses Vue 3 Composition API, `vue-router`, `vee-validate` for forms, and auto-generated API client from `@vc-shell/api-client-generator`.

The page designer is the full-screen `PageBuilderDesigner` blade (`vue/src/modules/pages/PageBuilderDesigner.vue`); its state, schema, document and preview code live in `vue/src/modules/designer/`. It edits database pages (param = page group id) and pages kept as files (query: storeId, path, type), with the git version history when the store keeps its pages in git. The preview is the storefront's `/designer-preview` page in an iframe, driven over postMessage (`designer/preview.ts`). The standalone app opens it from `/apps/page-builder-shell/?storeId=…&designGroup=…` or `&designPath=…&designType=…`, which is what the classic admin's page blade links to.

### AngularJS Admin Integration

Located at `Web/Scripts/`. Module registered as `virtoCommerce.pageBuilderModule`. Provides:
- Page editing blade (`blades/pages/edit-page.js`)
- Store detail widget (`widgets/pageBuilder-app-widget.js`)
- API resource (`resources/pageBuilderApi.js`)
- File upload handler (`services/page-builder-file.handler.js`)

This is **legacy code** — do not add new features here. It integrates with the VirtoCommerce admin blade architecture.

---

## CI/CD

- `.github/workflows/module-ci.yml` — main pipeline (Node 20, build + package NuGet)
- `.github/workflows/deploy.yml` — deployment
- `.github/workflows/publish-nugets.yml` — NuGet publishing
- `Jenkinsfile` — Jenkins pipeline
- Build output: `dist/template-builder/` — deployed as part of the .NET module
