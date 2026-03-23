# AngularJS Legacy Admin

## Description
Rules and architecture overview for the legacy AngularJS admin integration located in `Web/Scripts/`. Use this skill when reading or making minimal changes to the AngularJS blade code.

## Triggers
- Working with files in `src/VirtoCommerce.PageBuilderModule.Web/Scripts/`
- Questions about the admin blade UI or widgets
- File upload handling in the admin
- AngularJS `$resource` API wrapper

---

## Core Rule

**Do not add new features here.** This is legacy code integrated with the VirtoCommerce AngularJS admin shell. New functionality belongs in the Angular 20 designer app or the Vue 3 shell app.

Acceptable changes:
- Bug fixes
- Small UI adjustments required by the platform
- Updating API endpoints if the backend changes

---

## Module Registration

Registered as `virtoCommerce.pageBuilderModule` in the VC platform admin. The platform admin shell (`vc-platform`) loads this module automatically based on `module.manifest`.

---

## File Overview

| File | Purpose |
|------|---------|
| `blades/pages/edit-page.js` | Main blade controller for editing a page entry (name, permalink, template). Long controller — avoid adding to it. |
| `widgets/pageBuilder-app-widget.js` | Widget displayed on the Store detail blade. Opens the Page Builder app in an iframe. |
| `resources/pageBuilderApi.js` | `$resource` wrapper for the Page Builder REST API. Add new endpoints here if needed. |
| `services/page-builder-file.handler.js` | File upload handler. Plugs into the VC platform file upload infrastructure. |

---

## Blade Architecture (VC Platform pattern)

The VC admin uses a blade-based navigation system. Key concepts:

```javascript
// Opening a child blade
bladeNavigationService.showBlade({
    id: 'myBlade',
    title: 'My Title',
    controller: 'myController',
    template: 'path/to/template.html',
    parentBlade: blade
}, blade);

// Closing and refreshing parent
blade.parentBlade.refresh();
bladeNavigationService.closeBlade(blade);
```

Blades communicate via:
- `blade.parentBlade` reference
- Shared `$scope` or passed callback functions on the blade object

---

## API Resource Pattern

```javascript
// resources/pageBuilderApi.js
return $resource('api/pagebuilder/:id', { id: '@id' }, {
    get:    { method: 'GET' },
    save:   { method: 'POST' },
    delete: { method: 'DELETE' }
});
```

Inject as `pageBuilderApi` in controllers.

---

## AngularJS Patterns in Use

- `$scope` for view model (not `controllerAs`)
- `$resource` for HTTP
- `$translate` for i18n strings
- `bladeNavigationService` for blade navigation
- `platformWebApp.bladeUtils` for common blade utilities
- No ES6 classes — plain functions and `$scope` assignments
