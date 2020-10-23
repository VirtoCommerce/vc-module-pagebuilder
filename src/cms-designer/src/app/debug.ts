import { ActionReducer, MetaReducer } from '@ngrx/store';
import { environment } from 'src/environments/environment';

import { highlightInPreviewActionName, markSectionHoveredInPreviewActionName } from '@editor/store/editor.actions';

export const actionsToIgonre: string[] = [
    highlightInPreviewActionName,
    markSectionHoveredInPreviewActionName
];

export const debugInfo = {
    source: window.location.toString(),
    store: [],
    errors: []
};

export function debug(actionReducer: ActionReducer<any>): ActionReducer<any> {
    return function (state, action) {
        if (actionsToIgonre.indexOf(action.type) === -1) {
            debugInfo.store.push({ state, action })
            if (debugInfo.store.length > 10) {
                debugInfo.store.splice(0, 1);
            }
        }
        return actionReducer(state, action);
    };
}

export function logger(actionReducer: ActionReducer<any>): ActionReducer<any> {
    return function (state, action) {
        if (actionsToIgonre.indexOf(action.type) === -1) {
            console.log(state, action);
        }
        return actionReducer(state, action);
    };
}

export const metaReducers: MetaReducer<any>[] = environment.production ? [debug, logger] : [debug];
