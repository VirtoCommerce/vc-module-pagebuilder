import { ActionReducer, MetaReducer } from '@ngrx/store';
import { environment } from 'src/environments/environment';

import * as editorActions from '@editor/store/editor.actions';

export const actionsToIgonre: string[] = [
    editorActions.highlightInPreview.type,
    editorActions.markSectionHoveredInPreview.type
];

export const debugInfo = [];

function debug(actionReducer: ActionReducer<any>): ActionReducer<any> {
    return function (state, action) {
        if (actionsToIgonre.indexOf(action.type) === -1) {
            debugInfo.push({ state, action })
            if (debugInfo.length > 10) {
                debugInfo.splice(0, 1);
            }
        }
        return actionReducer(state, action);
    };
}

function logger(actionReducer: ActionReducer<any>): ActionReducer<any> {
    return function (state, action) {
        if (actionsToIgonre.indexOf(action.type) === -1) {
            console.log(state, action);
        }
        return actionReducer(state, action);
    };    
}

export const metaReducers: MetaReducer<any>[] = environment.production ? [debug, logger] : [debug];
