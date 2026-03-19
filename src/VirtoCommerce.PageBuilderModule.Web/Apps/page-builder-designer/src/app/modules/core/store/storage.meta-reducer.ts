import { InjectionToken } from '@angular/core';
import { ActionReducer, Action } from '@ngrx/store';
import { merge, pick } from 'lodash-es';
import { LocalStorageService } from './local-storage.service';

export function storageMetaReducer<S, A extends Action = Action>(saveKeys: string[],
    localStorageKey: string,
    storageService: LocalStorageService
) {
    let onInit = true;
    return function (reducer: ActionReducer<S, A>) {
        return function (state: S, action: A): S {
            const nextState = reducer(state, action);
            if (onInit) {
                onInit = false;
                const savedState = storageService.getItem(localStorageKey);
                return merge(nextState, savedState);
            }
            const stateToSave = pick(nextState, saveKeys);
            storageService.setItem(localStorageKey, stateToSave);

            return nextState;
        };
    };
}
