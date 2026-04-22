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
  private _cachedContext: any = null;

  private readonly env = inject(EnvironmentRef);
  private readonly cookies = inject(CookieService);
  private readonly evaluator = inject(EvaluatorService);

  private mergedConfig: any = {}; // raw config values exposed as 'config' in context
  private settings: any = {}; // evaluated proxy exposed as 'settings' in context

  initConfigWith(config: any) {
    Object.assign(this.mergedConfig, config);
    this._cachedContext = null; // reset context to new values
    for (const property of Object.keys(this.mergedConfig)) {
      Object.defineProperty(this.settings, property, {
        get: () => {
          return this.evaluator.evaluateProperty(this.mergedConfig, property, this.getContext());
        }
      });
    }
  }

  getValueByEntryType(property: OptionName, context: any = null, type: string | null = null): any {
    const source = this.getValue(property, context);
    return source[type ?? 'default'] ?? source['default'] ?? source;
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
    if (!this._cachedContext) {
      const params: any = {};
      const searchParams = new URLSearchParams(this.env.nativeWindow.location.search);
      searchParams.forEach((value, key) => {
        const allValues = searchParams.getAll(key);
        params[key] = allValues.length === 1 ? value : allValues;
      });
      const { hash, href, host, protocol, pathname, origin } = this.env.nativeWindow.location;
      const hashParts = hash?.split('?');
      const hashPath = hashParts?.[0] ?? null;
      const hashParams = hashParts?.[1] ? new URLSearchParams(hashParts[1]) : null;
      hashParams?.forEach((value, key) => {
        const allValues = hashParams.getAll(key);
        params[key] = allValues.length === 1 ? value : allValues;
      });

      this._cachedContext = {
        config: this.mergedConfig,
        settings: this.settings,
        location: {
          url: href, params: params, path: pathname,
          host, protocol, hash, hashPath, origin
        }
      };
    }
    return this._cachedContext;
  }

  private mergeContexts(additionalContext: any) {
    const result = { ...this.getContext(), ...additionalContext };
    Object.defineProperty(result, 'sessionId', {
      get: () => {
        return this.getCurrentSessionId();
      }
    });
    return result;
  }

  getCurrentSessionId(): string {
    return this.cookies.check(this.SESSION_ID)
      ? this.cookies.get(this.SESSION_ID)
      : this.generatePrefixAndSetCookie();
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
  | 'previewImpersonation'
  | 'previewAccounts'
  | 'ozAgentUrl'
  ;
