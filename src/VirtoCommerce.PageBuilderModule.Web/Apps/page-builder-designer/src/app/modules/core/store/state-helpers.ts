import { InjectionToken } from '@angular/core';
import { Action, StoreConfig } from '@ngrx/store';
import { LocalStorageService } from './local-storage.service';
import { storageMetaReducer } from './storage.meta-reducer';

function getConfig(saveKeys: string[], localStorageKey: string, storageService: LocalStorageService) {
    return { metaReducers: [storageMetaReducer(saveKeys, localStorageKey, storageService)] };
}

export function CreateStorageProviders<T>(
    key: string,
    statePropertyName: string,
    properties: string[]): { config: InjectionToken<StoreConfig<T, Action>>; providers: any[] } {

    const STORAGE_KEYS = new InjectionToken<keyof T[]>(`${key}StorageKeys`);
    const LOCAL_STORAGE_KEY = new InjectionToken<string[]>(`${key}Storage`);
    const CONFIG_TOKEN = new InjectionToken<StoreConfig<T, Action>>(`${key}ConfigToken`);

    return {
        config: CONFIG_TOKEN,
        providers: [
            { provide: LOCAL_STORAGE_KEY, useValue: statePropertyName },
            { provide: STORAGE_KEYS, useValue: properties },
            {
                provide: CONFIG_TOKEN,
                deps: [STORAGE_KEYS, LOCAL_STORAGE_KEY, LocalStorageService],
                useFactory: getConfig
            }
        ]
    };
}
