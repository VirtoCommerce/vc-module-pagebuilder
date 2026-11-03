import { TestBed } from '@angular/core/testing';
import { provideMockActions } from '@ngrx/effects/testing';
import { provideMockStore, MockStore } from '@ngrx/store/testing';
import { ReplaySubject, firstValueFrom } from 'rxjs';
import { take, toArray } from 'rxjs/operators';
import { Action } from '@ngrx/store';

import { ThemeDomainEffects } from './effects';
import * as actions from '../actions';
import * as sharedActions from '@shared/store/actions';
import * as routingActions from '@shared/routing/actions';
import * as domainSelectors from '../selectors';
import * as routingSelectors from '@shared/routing';

describe('ThemeDomainEffects', () => {
    let effects: ThemeDomainEffects;
    let actions$: ReplaySubject<Action>;
    let store: MockStore;

    beforeEach(() => {
        actions$ = new ReplaySubject<Action>(1);

        TestBed.configureTestingModule({
            providers: [
                ThemeDomainEffects,
                provideMockActions(() => actions$),
                provideMockStore({
                    selectors: [
                        { selector: domainSelectors.selectOpenedGroups, value: ['colors'] },
                        { selector: domainSelectors.selectPresets, value: { dark: { color: 'black' }, light: { color: 'white' } } },
                        { selector: domainSelectors.selectCurrentSettings, value: { color: 'blue' } },
                        { selector: routingSelectors.selectPresetParameter, value: '' },
                    ],
                }),
            ],
        });

        store = TestBed.inject(MockStore);
        effects = TestBed.inject(ThemeDomainEffects);
    });

    afterEach(() => store.resetSelectors());

    describe('toggleGroup$', () => {
        it('adds group to URL when not present', async () => {
            store.overrideSelector(domainSelectors.selectOpenedGroups, ['fonts']);
            store.refreshState();

            actions$.next(actions.toggleGroup({ group: { name: 'colors' } as any }));
            const result = await firstValueFrom(effects.toggleGroup$);
            expect(result.type).toBe(routingActions.go.type);
            expect((result as any).queryParams.groups).toContain('colors');
        });

        it('removes group from URL when present', async () => {
            actions$.next(actions.toggleGroup({ group: { name: 'colors' } as any }));
            const result = await firstValueFrom(effects.toggleGroup$);
            expect(result.type).toBe(routingActions.go.type);
            // When all groups removed, join returns '' which becomes undefined via || undefined
            expect((result as any).queryParams.groups).toBeUndefined();
        });
    });

    describe('presetApplied$', () => {
        it('shows success notification', async () => {
            actions$.next(actions.applyPreset({ preset: 'dark' }));
            const result = await firstValueFrom(effects.presetApplied$);
            expect(result.type).toBe(sharedActions.showNotification.type);
            expect((result as any).msgType).toBe('success');
        });
    });

    describe('saveSettingsSuccess$', () => {
        it('shows success notification', async () => {
            actions$.next(actions.saveSettingsSuccess());
            const result = await firstValueFrom(effects.saveSettingsSuccess$);
            expect(result.type).toBe(sharedActions.showNotification.type);
            expect((result as any).message).toContain('successfully saved');
        });
    });

    describe('saveSettingsFail$', () => {
        it('shows error notification', async () => {
            actions$.next(actions.saveSettingsFail({ error: {} as any }));
            const result = await firstValueFrom(effects.saveSettingsFail$);
            expect(result.type).toBe(sharedActions.showNotification.type);
            expect((result as any).msgType).toBe('error');
        });
    });

    describe('cancelAction$', () => {
        it('reverts changes and exits settings', async () => {
            actions$.next(actions.executeAction({ action: 'cancel' }));
            const results = await firstValueFrom(effects.cancelAction$.pipe(take(3), toArray()));
            const types = results.map(r => r.type);
            expect(types).toContain(actions.revertChanges.type);
            expect(types).toContain(actions.exitSettings.type);
            expect(types).toContain(actions.updateInPreview.type);
        });
    });

    describe('revertChanges$', () => {
        it('shows revert notification', async () => {
            actions$.next(actions.revertChanges());
            const result = await firstValueFrom(effects.revertChanges$);
            expect(result.type).toBe(sharedActions.showNotification.type);
            expect((result as any).message).toContain('reverted');
        });
    });

    describe('applyAction$', () => {
        it('dispatches saveSettings on save action', async () => {
            actions$.next(actions.executeAction({ action: 'save' }));
            const results = await firstValueFrom(effects.applyAction$.pipe(take(1), toArray()));
            expect(results[0].type).toBe(actions.saveSettings.type);
        });
    });

    describe('successfullSaved$', () => {
        it('applies changes and exits settings', async () => {
            actions$.next(actions.saveSettingsSuccess());
            const results = await firstValueFrom(effects.successfullSaved$.pipe(take(2), toArray()));
            const types = results.map(r => r.type);
            expect(types).toContain(actions.applyChanges.type);
            expect(types).toContain(actions.exitSettings.type);
        });
    });

    describe('updateInPreview$', () => {
        it('broadcasts settings to preview', async () => {
            actions$.next(actions.updateInPreview({ settings: { color: 'red' } }));
            const result = await firstValueFrom(effects.updateInPreview$);
            expect(result.type).toBe(sharedActions.broadcastPreviewMessage.type);
            expect((result as any).msg.type).toBe('settings');
            expect((result as any).msg.settings).toEqual({ color: 'red' });
        });

        it('falls back to current settings when null', async () => {
            actions$.next(actions.updateInPreview({ settings: null }));
            const result = await firstValueFrom(effects.updateInPreview$);
            expect((result as any).msg.settings).toEqual({ color: 'blue' });
        });
    });

    describe('previewPreset$', () => {
        it('dispatches updateInPreview with preset settings', async () => {
            actions$.next(actions.previewPreset({ preset: 'dark' }));
            const result = await firstValueFrom(effects.previewPreset$);
            expect(result.type).toBe(actions.updateInPreview.type);
            expect((result as any).settings).toEqual({ color: 'black' });
        });
    });
});
