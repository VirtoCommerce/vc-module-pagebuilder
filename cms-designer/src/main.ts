import { enableProdMode, ApplicationRef } from '@angular/core';
import { platformBrowserDynamic } from '@angular/platform-browser-dynamic';

import { AppModule } from './app/app.module';
import { environment } from './environments/environment';
import { enableDebugTools } from '@angular/platform-browser';

import { debugInfo } from './app/debug';

if (environment.production) {
    enableProdMode();
}

platformBrowserDynamic().bootstrapModule(AppModule)
    .then(moduleRef => {
        if (!environment.production) {
            const appRef = moduleRef.injector.get(ApplicationRef);
            const componentRef = appRef.components[0];
            enableDebugTools(componentRef);
        }
    })
    .catch(err => {
        debugInfo.errors.push(err);
        console.log(err);
    });

window.onerror = function (message, file, line, col, error) {
    debugInfo.errors.push(error);
    return false;
};

window.addEventListener("error", function (e) {
    debugInfo.errors.push(e);
    return false;
});

window.addEventListener('unhandledrejection', function (e) {
    debugInfo.errors.push(e);
});
