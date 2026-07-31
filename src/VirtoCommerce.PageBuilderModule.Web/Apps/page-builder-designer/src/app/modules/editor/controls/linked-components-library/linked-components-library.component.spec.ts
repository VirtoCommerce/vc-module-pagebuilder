import { TestBed } from '@angular/core/testing';
import { MockStore, provideMockStore } from '@ngrx/store/testing';

import { AppConfig } from '@integration/services';
import { LinkedComponent } from '@editor/models';
import * as actions from '@editor/store/actions';
import * as selectors from '@editor/store/selectors';
import * as routingSelectors from '@shared/routing/selectors';

import { LinkedComponentsLibraryComponent } from './linked-components-library.component';

describe('LinkedComponentsLibraryComponent', () => {
    const component: LinkedComponent = {
        id: 'component-1',
        storeId: 'store-1',
        name: 'USP bar',
        usageCount: 4,
        usagePages: [],
    };
    let store: MockStore;

    beforeEach(() => {
        TestBed.configureTestingModule({
            imports: [LinkedComponentsLibraryComponent],
            providers: [
                provideMockStore({
                    selectors: [
                        {
                            selector: selectors.selectLinkedComponentsSearchView,
                            value: {
                                keyword: '',
                                resultIds: [component.id],
                                results: [component],
                                optimisticResultIds: [],
                                loadedCount: 1,
                                totalCount: 1,
                                loading: false,
                                rebasePending: false,
                                error: null,
                            },
                        },
                        { selector: routingSelectors.selectInsertIndexParameter, value: 2 },
                    ],
                }),
                { provide: AppConfig, useValue: { getValue: vi.fn().mockReturnValue(true) } },
            ],
        });
        store = TestBed.inject(MockStore);
    });

    it('loads the library and renders component usage', () => {
        const dispatch = vi.spyOn(store, 'dispatch');
        const fixture = TestBed.createComponent(LinkedComponentsLibraryComponent);

        fixture.detectChanges();

        expect(dispatch).toHaveBeenCalledWith(actions.searchLinkedComponents({ keyword: '' }));
        expect(fixture.nativeElement.textContent).toContain('SHARED BLOCKS LIBRARY');
        expect(fixture.nativeElement.textContent).toContain('USP bar');
        expect(fixture.nativeElement.textContent).toContain('4 pages');
        const heading = fixture.nativeElement.querySelector('.library-heading') as HTMLButtonElement;
        expect(heading.getAttribute('aria-expanded')).toBe('true');
        expect(fixture.nativeElement.querySelector(`#${heading.getAttribute('aria-controls')}`)).toBeTruthy();
    });

    it('asks for insertion semantics before dispatching an insert', () => {
        const dispatch = vi.spyOn(store, 'dispatch');
        const fixture = TestBed.createComponent(LinkedComponentsLibraryComponent);
        fixture.detectChanges();
        dispatch.mockClear();

        fixture.componentInstance.chooseInsertionMode(component, new MouseEvent('click'));

        expect(dispatch).toHaveBeenCalledWith(actions.chooseLinkedComponentInsertionMode({
            componentId: component.id,
            insertIndex: 2,
            defaultMode: 'linked',
        }));
    });

    it('requests the next server page when more components are available', () => {
        const firstHundredIds = Array.from({ length: 100 }, (_, index) => `component-${index + 1}`);
        store.overrideSelector(selectors.selectLinkedComponentsSearchView, {
            keyword: 'hero',
            resultIds: firstHundredIds,
            results: [component],
            optimisticResultIds: [],
            loadedCount: 100,
            totalCount: 101,
            loading: false,
            rebasePending: false,
            error: null,
        });
        store.refreshState();
        const dispatch = vi.spyOn(store, 'dispatch');
        const fixture = TestBed.createComponent(LinkedComponentsLibraryComponent);
        fixture.detectChanges();
        dispatch.mockClear();

        fixture.componentInstance.loadMore();

        expect(dispatch).toHaveBeenCalledWith(actions.searchLinkedComponents({ keyword: 'hero', skip: 100 }));
    });

    it('retries a failed load-more request from the same offset', () => {
        store.overrideSelector(selectors.selectLinkedComponentsSearchView, {
            keyword: 'hero',
            resultIds: ['component-1'],
            results: [component],
            optimisticResultIds: [],
            loadedCount: 1,
            totalCount: 2,
            loading: false,
            rebasePending: false,
            error: 'Request failed',
        });
        store.refreshState();
        const dispatch = vi.spyOn(store, 'dispatch');
        const fixture = TestBed.createComponent(LinkedComponentsLibraryComponent);
        fixture.detectChanges();
        dispatch.mockClear();

        fixture.componentInstance.retry();

        expect(dispatch).toHaveBeenCalledWith(actions.retryLinkedComponentsSearch({ keyword: 'hero', skip: 1 }));
        expect(fixture.nativeElement.querySelector('[role="alert"]')?.textContent).toContain('Request failed');
        expect(fixture.nativeElement.textContent).toContain('Retry');
    });

    it('retries an interrupted optimistic rebase from the first page', async () => {
        store.overrideSelector(selectors.selectLinkedComponentsSearchView, {
            keyword: 'hero',
            resultIds: [component.id],
            results: [component],
            optimisticResultIds: [component.id],
            loadedCount: 1,
            totalCount: 3,
            loading: false,
            rebasePending: true,
            error: 'Refresh failed',
        });
        store.refreshState();
        const dispatch = vi.spyOn(store, 'dispatch');
        const fixture = TestBed.createComponent(LinkedComponentsLibraryComponent);
        await fixture.whenStable();
        dispatch.mockClear();

        fixture.componentInstance.retry();

        expect(dispatch).toHaveBeenCalledWith(actions.refreshLinkedComponentsSearch({ keyword: 'hero' }));
    });
});
