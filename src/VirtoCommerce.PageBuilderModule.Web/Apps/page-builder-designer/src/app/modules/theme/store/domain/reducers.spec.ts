import { themeDomainReducers } from './reducers';
import { initialState } from './state';
import * as actions from '../actions';

describe('themeDomainReducers', () => {
    it('returns initial state for unknown action', () => {
        const state = themeDomainReducers(undefined, { type: '@@INIT' });
        expect(state).toEqual(initialState);
        expect(state.isDirty).toBe(false);
    });

    describe('updateSettings', () => {
        it('sets isDirty to true', () => {
            const state = themeDomainReducers(initialState, actions.updateSettings({ model: { color: 'red' } as any } as any));
            expect(state.isDirty).toBe(true);
        });
    });

    describe('applyPreset', () => {
        it('sets isDirty to true', () => {
            const state = themeDomainReducers(initialState, actions.applyPreset({ preset: 'dark' }));
            expect(state.isDirty).toBe(true);
        });
    });

    describe('revertChanges', () => {
        it('sets isDirty to false', () => {
            const prev = { ...initialState, isDirty: true };
            const state = themeDomainReducers(prev, actions.revertChanges());
            expect(state.isDirty).toBe(false);
        });
    });

    describe('applyChanges', () => {
        it('sets isDirty to false', () => {
            const prev = { ...initialState, isDirty: true };
            const state = themeDomainReducers(prev, actions.applyChanges());
            expect(state.isDirty).toBe(false);
        });
    });
});
