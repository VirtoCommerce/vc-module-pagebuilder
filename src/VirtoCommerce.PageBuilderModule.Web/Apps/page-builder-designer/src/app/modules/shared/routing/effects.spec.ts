import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Location } from '@angular/common';
import { provideMockActions } from '@ngrx/effects/testing';
import { provideMockStore, MockStore } from '@ngrx/store/testing';
import { ReplaySubject } from 'rxjs';
import { Action } from '@ngrx/store';

import { RoutingEffects } from './effects';
import * as actions from './actions';
import * as fromRoute from './selectors';

describe('RoutingEffects', () => {
    let effects: RoutingEffects;
    let actions$: ReplaySubject<Action>;
    let router: { navigate: ReturnType<typeof vi.fn> };
    let location: { back: ReturnType<typeof vi.fn>; forward: ReturnType<typeof vi.fn> };
    let store: MockStore;

    beforeEach(() => {
        actions$ = new ReplaySubject<Action>(1);
        router = { navigate: vi.fn() };
        location = { back: vi.fn(), forward: vi.fn() };

        TestBed.configureTestingModule({
            providers: [
                RoutingEffects,
                provideMockActions(() => actions$),
                provideMockStore({
                    selectors: [
                        { selector: fromRoute.selectPath, value: '/pages' },
                        { selector: fromRoute.selectQueryParams, value: { type: 'page' } },
                        { selector: fromRoute.isEmpty, value: false },
                        { selector: fromRoute.selectTypeParameter, value: 'page' },
                        { selector: fromRoute.selectPathParameter, value: '/home.json' },
                        { selector: fromRoute.selectParentTemplateParameter, value: '' },
                        { selector: fromRoute.selectPreviewModeParameter, value: '' },
                        { selector: fromRoute.selectGroupIdParameter, value: '' },
                    ],
                }),
                { provide: Router, useValue: router },
                { provide: Location, useValue: location },
            ],
        });

        store = TestBed.inject(MockStore);
        effects = TestBed.inject(RoutingEffects);
    });

    afterEach(() => store.resetSelectors());

    describe('navigate$', () => {
        it('calls router.navigate with merged params', () => {
            actions$.next(actions.go({ path: ['/pages/edit'], queryParams: { id: '1' } }));

            effects.navigate$.subscribe();

            expect(router.navigate).toHaveBeenCalledWith(
                ['/pages/edit'],
                expect.objectContaining({
                    queryParams: expect.objectContaining({ type: 'page', id: '1' }),
                })
            );
        });

        it('preserves current path when no path provided', () => {
            actions$.next(actions.go({ queryParams: { filter: 'test' } }));

            effects.navigate$.subscribe();

            expect(router.navigate).toHaveBeenCalledWith(
                ['/pages'],
                expect.objectContaining({
                    queryParams: expect.objectContaining({ type: 'page', filter: 'test' }),
                })
            );
        });

        it('does not navigate when isEmpty is true', () => {
            store.overrideSelector(fromRoute.isEmpty, true);
            store.refreshState();

            actions$.next(actions.go({ path: ['/pages'] }));
            effects.navigate$.subscribe();

            expect(router.navigate).not.toHaveBeenCalled();
        });
    });

    describe('navigateToOtherModule$', () => {
        it('calls router.navigate with route params preserved', () => {
            actions$.next(actions.jump({ path: ['/themes'], queryParams: { groups: 'colors' } }));

            effects.navigateToOtherModule$.subscribe();

            expect(router.navigate).toHaveBeenCalledWith(
                ['/themes'],
                expect.objectContaining({
                    queryParams: expect.objectContaining({
                        type: 'page',
                        path: '/home.json',
                        groups: 'colors',
                    }),
                })
            );
        });
    });

    describe('navigateBack$', () => {
        it('calls location.back', () => {
            actions$.next(actions.back());
            effects.navigateBack$.subscribe();
            expect(location.back).toHaveBeenCalled();
        });
    });

    describe('navigateForward$', () => {
        it('calls location.forward', () => {
            actions$.next(actions.forward());
            effects.navigateForward$.subscribe();
            expect(location.forward).toHaveBeenCalled();
        });
    });
});
