import { inject, Injectable } from '@angular/core';

import { CookieService } from 'ngx-cookie-service';

import { appHelpers } from '../helpers';

import { EnvironmentRef } from './environment.ref';
import { EvaluatorService } from './evaluator.service';

@Injectable({
    providedIn: 'root'
})
export class AppConfig {
    private readonly SESSION_ID = 'sessionId';
    private _context: any = null;

    private readonly env = inject(EnvironmentRef);
    private readonly cookies = inject(CookieService);
    private readonly evaluator = inject(EvaluatorService);

    // todo: check comments that it is true!
    private mergedConfig: any = {}; // 'config' in context, properties will be evaluated
    private settings: any = {}; // 'settings' in context, properties will not be evaluated

    initConfigWith(config: any) {
        Object.keys(config).forEach(key => {
            this.mergedConfig[key] = config[key];
        });
        this._context = null; // reset context to new values
        for (const property of Object.keys(this.mergedConfig)) {
            Object.defineProperty(this.settings, property, {
                get: () => {
                    return this.evaluator.evaluateProperty(this.mergedConfig, property, this.context);
                }
            });
        }
    }

    getValueByEntryType(property: OptionName, context: any = null, type: string | null = null): any {
        const source = this.getValue(property, context);
        return source[type || 'default'] || source['default'] || source;
    }

    getValue(property: OptionName, context: any = null): any {
        if (!context) {
            return this.settings[property];
        } else {
            const mergedContext = this.mergeContexts(context);
            return this.evaluator.evaluateProperty(this.mergedConfig, property, mergedContext);
        }
    }

    getContext(): any {
        return this.context;
    }

    private mergeContexts(additionalContext: any) {
        const result = { ...this.context, ...additionalContext };
        Object.defineProperty(result, 'sessionId', {
            get: () => {
                return this.getCurrentSessionId();
            }
        });
        return result;
    }

    private get context(): any {
        if (!this._context) {
            const params: any = {};
            const searchParams = new URLSearchParams(this.env.nativeWindow.location.search);
            for (const p of <any>searchParams) {
                const allValues = searchParams.getAll(p[0]);
                params[p[0]] = allValues.length === 1 ? p[1] : allValues;
            }
            const { hash, href, host, protocol, pathname, origin } = this.env.nativeWindow.location;
            const [hashPath, hashParams] = hash && hash.split('?').length > 1 ?
                [hash.split('?')[0], new URLSearchParams(hash.split('?')[1])] :
                [null, null];
            if (hashParams) {
                for (const p of <any>hashParams) {
                    const allValues = hashParams.getAll(p[0]);
                    params[p[0]] = allValues.length === 1 ? p[1] : allValues;
                }
            }

            this._context = {
                config: this.mergedConfig,
                settings: this.settings,
                location: {
                    url: href, params: params, path: pathname,
                    host, protocol, hash, hashPath, origin
                }
            };
        }
        return this._context;
    }

    getCurrentSessionId(): string {
        const result = this.cookies.check(this.SESSION_ID)
            ? this.cookies.get(this.SESSION_ID)
            : this.generatePrefixAndSetCookie();
        return result;
    }

    private generatePrefixAndSetCookie(): string {
        const result = appHelpers.generateUniqueString(10);
        this.cookies.set(this.SESSION_ID, result);
        return result;
    }
}

export type OptionName = 'templatesListUrl'
    | 'sectionsListUrl'
    | 'templateUrl'
    | 'saveTemplates'
    | 'settingsDataRequest'
    | 'settingsSchemaRequest'
    | 'saveSettings'
    | 'uploadAssetsRequest'
    | 'fullPreviewUrl'
    | 'skipTheme'
    | 'skipTemplates'
    | 'assetsUrlTemplate'
    | 'publish'
    | 'externalPreview'
    | 'publishPages'
    | 'saveGroupedPage'
    ;
