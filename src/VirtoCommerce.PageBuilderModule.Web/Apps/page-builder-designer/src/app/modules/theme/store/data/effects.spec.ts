import { TestBed } from '@angular/core/testing';
import { provideMockActions } from '@ngrx/effects/testing';
import { provideMockStore, MockStore } from '@ngrx/store/testing';
import { ReplaySubject, of, firstValueFrom, throwError } from 'rxjs';
import { take, toArray } from 'rxjs/operators';
import { Action } from '@ngrx/store';

import * as fromRoute from '@shared/routing';
import * as shared from '@shared/store/actions';

import { ThemeDataEffects } from './effects';
import * as actions from '../actions';
import * as selectors from '../selectors';

describe('ThemeDataEffects', () => {
    let effects: ThemeDataEffects;
    let actions$: ReplaySubject<Action>;
    let store: MockStore;
    let service: {
        loadSettingsData: ReturnType<typeof vi.fn>;
        loadSettingsSchema: ReturnType<typeof vi.fn>;
        saveSettings: ReturnType<typeof vi.fn>;
    };

    beforeEach(() => {
        actions$ = new ReplaySubject<Action>(1);
        service = {
            loadSettingsData: vi.fn().mockReturnValue(of({ current: {}, presets: {} })),
            loadSettingsSchema: vi.fn().mockReturnValue(of([])),
            saveSettings: vi.fn().mockReturnValue(of(null)),
        };

        TestBed.configureTestingModule({
            providers: [
                ThemeDataEffects,
                provideMockActions(() => actions$),
                provideMockStore({
                    selectors: [
                        { selector: selectors.selectCurrentSettings, value: null },
                        { selector: selectors.selectLoadedSettingsSchema, value: null },
                        { selector: selectors.selectCurrentSettingsDataModel, value: { current: {}, presets: {} } },
                        { selector: fromRoute.selectDataParams, value: { module: 'theme' } },
                    ],
                }),
                { provide: 'ThemeSettingsService', useValue: service },
            ],
        });

        // ThemeSettingsService is injected via inject(), override it
        store = TestBed.inject(MockStore);
        effects = TestBed.inject(ThemeDataEffects);
        // Replace service reference
        (effects as any).service = service;
    });

    afterEach(() => store.resetSelectors());

    describe('raiseLoadData$', () => {
        it('dispatches load actions when settings is null', async () => {
            actions$.next(actions.raiseLoadData());
            const results = await firstValueFrom(effects.raiseLoadData$.pipe(take(2), toArray()));
            const types = results.map(r => r.type);
            expect(types).toContain(actions.loadSettingsData.type);
            expect(types).toContain(actions.loadSettingsSchema.type);
        });

        it('does not load when settings already exists', async () => {
            store.overrideSelector(selectors.selectCurrentSettings, { color: 'blue' });
            store.refreshState();

            actions$.next(actions.raiseLoadData());

            // Effect should filter out — give it time to process
            const results: Action[] = [];
            const sub = effects.raiseLoadData$.subscribe(a => results.push(a));
            await new Promise(r => setTimeout(r, 50));
            sub.unsubscribe();
            expect(results.length).toBe(0);
        });
    });

    // Restoring an expired session does not change the route, so nothing re-enters the module and
    // the requests dropped with the session have to be repeated explicitly (VCST-5847).
    describe('reloadAfterSignIn$', () => {
        it('repeats both requests when neither of them made it', async () => {
            actions$.next(shared.initShared());
            const results = await firstValueFrom(effects.reloadAfterSignIn$.pipe(take(2), toArray()));
            const types = results.map(r => r.type);
            expect(types).toContain(actions.loadSettingsData.type);
            expect(types).toContain(actions.loadSettingsSchema.type);
        });

        it('repeats only the request that is still missing', async () => {
            store.overrideSelector(selectors.selectCurrentSettings, { color: 'blue' });
            store.refreshState();

            actions$.next(shared.initShared());
            const results = await firstValueFrom(effects.reloadAfterSignIn$.pipe(take(1), toArray()));
            expect(results.map(r => r.type)).toEqual([actions.loadSettingsSchema.type]);
        });

        it('stays out of the way while another module is on screen', async () => {
            store.overrideSelector(fromRoute.selectDataParams, { module: 'editor' });
            store.refreshState();

            actions$.next(shared.initShared());

            const results: Action[] = [];
            const sub = effects.reloadAfterSignIn$.subscribe(a => results.push(a));
            await new Promise(r => setTimeout(r, 50));
            sub.unsubscribe();
            expect(results.length).toBe(0);
        });
    });

    describe('loadSettingsData$', () => {
        it('dispatches success on successful load', async () => {
            const data = { current: { color: 'blue' }, presets: {} };
            service.loadSettingsData.mockReturnValue(of(data));

            actions$.next(actions.loadSettingsData());
            const result = await firstValueFrom(effects.loadSettingsData$);
            expect(result.type).toBe(actions.loadSettingsDataSuccess.type);
            expect((result as any).settingsData).toBe(data);
        });

        it('dispatches fail on error', async () => {
            service.loadSettingsData.mockReturnValue(throwError(() => ({ message: 'fail' })));

            actions$.next(actions.loadSettingsData());
            const result = await firstValueFrom(effects.loadSettingsData$);
            expect(result.type).toBe(actions.loadSettingsDataFail.type);
        });
    });

    describe('loadSettingsSchema$', () => {
        it('dispatches success on successful load', async () => {
            const schema = [{ name: 'Colors' }];
            service.loadSettingsSchema.mockReturnValue(of(schema));

            actions$.next(actions.loadSettingsSchema());
            const result = await firstValueFrom(effects.loadSettingsSchema$);
            expect(result.type).toBe(actions.loadSettingsSchemaSuccess.type);
            expect((result as any).schema).toBe(schema);
        });
    });

    describe('saveSettings$', () => {
        it('dispatches success on successful save', async () => {
            service.saveSettings.mockReturnValue(of(null));

            actions$.next(actions.saveSettings());
            const result = await firstValueFrom(effects.saveSettings$);
            expect(result.type).toBe(actions.saveSettingsSuccess.type);
        });

        it('dispatches fail on error', async () => {
            service.saveSettings.mockReturnValue(throwError(() => ({ message: 'fail' })));

            actions$.next(actions.saveSettings());
            const result = await firstValueFrom(effects.saveSettings$);
            expect(result.type).toBe(actions.saveSettingsFail.type);
        });
    });

    describe('mergeServerSettingsSchema$', () => {
        it('dispatches useSettingsSchema', async () => {
            const schema = [{ name: 'Colors' }] as any;
            actions$.next(actions.loadSettingsSchemaSuccess({ schema }));
            const result = await firstValueFrom(effects.mergeServerSettingsSchema$);
            expect(result.type).toBe(actions.useSettingsSchema.type);
            expect((result as any).schema).toBe(schema);
        });
    });
});
