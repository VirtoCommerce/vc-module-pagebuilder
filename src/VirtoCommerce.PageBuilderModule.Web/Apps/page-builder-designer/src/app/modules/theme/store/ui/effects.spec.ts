import { TestBed } from '@angular/core/testing';
import { provideMockActions } from '@ngrx/effects/testing';
import { provideMockStore, MockStore } from '@ngrx/store/testing';
import { ReplaySubject, firstValueFrom } from 'rxjs';
import { take, toArray } from 'rxjs/operators';
import { Action } from '@ngrx/store';

import { ThemeUiEffects } from './effects';
import * as actions from '../actions';
import * as routingActions from '@shared/routing/actions';
import * as routingSelectors from '@shared/routing';

describe('ThemeUiEffects', () => {
    let effects: ThemeUiEffects;
    let actions$: ReplaySubject<Action>;
    let store: MockStore;

    beforeEach(() => {
        actions$ = new ReplaySubject<Action>(1);

        TestBed.configureTestingModule({
            providers: [
                ThemeUiEffects,
                provideMockActions(() => actions$),
                provideMockStore({
                    selectors: [
                        { selector: routingSelectors.selectPresetParameter, value: '' },
                    ],
                }),
            ],
        });

        store = TestBed.inject(MockStore);
        effects = TestBed.inject(ThemeUiEffects);
    });

    afterEach(() => store.resetSelectors());

    describe('gotoPresets$', () => {
        it('navigates to /themes/presets', async () => {
            actions$.next(actions.gotoPresets());
            const result = await firstValueFrom(effects.gotoPresets$);
            expect(result.type).toBe(routingActions.go.type);
            expect((result as any).path).toEqual(['/themes/presets']);
        });
    });

    describe('gotoPreviewPreset$', () => {
        it('navigates with preset query param', async () => {
            actions$.next(actions.previewPreset({ preset: 'dark' }));
            const result = await firstValueFrom(effects.gotoPreviewPreset$);
            expect(result.type).toBe(routingActions.go.type);
            expect((result as any).queryParams).toEqual({ preset: 'dark' });
        });
    });

    describe('exitPresets$', () => {
        it('navigates to /themes and clears preset and preview', async () => {
            actions$.next(actions.exitPresets());
            const results = await firstValueFrom(effects.exitPresets$.pipe(take(2), toArray()));
            expect(results[0].type).toBe(routingActions.go.type);
            expect((results[0] as any).path).toEqual(['/themes']);
            expect(results[1].type).toBe(actions.updateInPreview.type);
            expect((results[1] as any).settings).toBeNull();
        });
    });

    describe('exitSettings$', () => {
        it('jumps to /pages', async () => {
            actions$.next(actions.exitSettings());
            const result = await firstValueFrom(effects.exitSettings$);
            expect(result.type).toBe(routingActions.jump.type);
            expect((result as any).path).toEqual(['/pages']);
        });
    });

    describe('notifySettingsChanged$', () => {
        it('dispatches updateInPreview with model', async () => {
            actions$.next(actions.updateSettings({ model: { color: 'red' } } as any));
            const result = await firstValueFrom(effects.notifySettingsChanged$);
            expect(result.type).toBe(actions.updateInPreview.type);
            expect((result as any).settings).toEqual({ color: 'red' });
        });
    });
});
