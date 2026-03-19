import { Injectable, Type } from "@angular/core";

import { BaseControlDirective } from './base-control.directive';
import { UnknownEditorComponent } from './unknown-editor/unknown-editor.component';

type LazyLoader = () => Promise<Type<BaseControlDirective<any>>>;

@Injectable({
    providedIn: 'root'
})
export class ControlsFactory {
    private controls: { [key: string]: Type<BaseControlDirective<any>> } = {};
    private lazyLoaders: { [key: string]: LazyLoader } = {};

    register(type: string, component: Type<BaseControlDirective<any>>): void {
        this.controls[type] = component;
    }

    registerLazy(type: string, loader: LazyLoader): void {
        this.lazyLoaders[type] = loader;
    }

    resolve(type: string): Type<any> {
        return this.controls[type] ?? UnknownEditorComponent;
    }

    async resolveAsync(type: string): Promise<Type<any>> {
        if (this.controls[type]) {
            return this.controls[type];
        }
        if (this.lazyLoaders[type]) {
            const component = await this.lazyLoaders[type]();
            this.controls[type] = component; // cache after first load
            return component;
        }
        return UnknownEditorComponent;
    }

    isLazy(type: string): boolean {
        return !this.controls[type] && !!this.lazyLoaders[type];
    }
}
