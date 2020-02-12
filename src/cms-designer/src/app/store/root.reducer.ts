import { createReducer, on, Action } from '@ngrx/store';
import * as Actions from './root.actions';

export interface RootState {
    previewLoading: boolean;
    primaryFrameId: string;
    secondaryFrameId: string;
    primaryLoaded: boolean;
    secondaryLoaded: boolean;
    activeTabIndex: number;
    previewUrl: string;
    previewError: boolean;
}

const initialState: RootState = {
    previewLoading: false,
    primaryFrameId: null,
    secondaryFrameId: null,
    primaryLoaded: false,
    secondaryLoaded: false,
    activeTabIndex: 0,
    previewUrl: null,
    previewError: false
};

const rootReducers = createReducer(
    initialState,
    on(Actions.previewLoading, (state, { isLoading }) => ({ ...state, previewLoading: isLoading, previewError: false })),
    on(Actions.previewError, state => ({ ...state,
        primaryLoaded: false, secondaryLoaded: false, previewError: true, primaryFrameId: null, secondaryFrameId: null })),
    on(Actions.reloadPreview, state => ({...state,
        primaryLoaded: false, secondaryLoaded: false, previewError: false, previewLoading: true,
        primaryFrameId: null, secondaryFrameId: null})),
    on(Actions.previewReady, (state, {frameId}) => {
        if (!frameId) {
            return state;
        }
        const newValues: Partial<RootState> = {};
        if (!state.primaryFrameId) {
            newValues.primaryFrameId = frameId;
            newValues.primaryLoaded = true;
        } else if (!state.secondaryFrameId) {
            newValues.secondaryFrameId = frameId;
            newValues.secondaryLoaded = true;
        }
        return { ...state, ...newValues, previewError: false };
    }),
    on(Actions.toggleFrames, state => {
        const newValues: Partial<RootState> = {};
        newValues.primaryFrameId = state.secondaryFrameId;
        newValues.secondaryFrameId = state.primaryFrameId;
        console.log('toggle', newValues.primaryFrameId, 'is primary now');
        return {...state, ...newValues};
    }),
    on(Actions.tabIndexChanged, (state, {tabIndex}) => ({ ...state, activeTabIndex: tabIndex })),
    on(Actions.setPreviewUrl, (state, { url }) => ({ ...state, previewUrl: url }))
);

export function reducer(state: RootState, action: Action) {
    return rootReducers(state, action);
}
