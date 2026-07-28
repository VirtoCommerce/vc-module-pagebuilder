import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Store } from '@ngrx/store';

import { IconComponent } from '@core/components/icon/icon.component';
import { AppConfig } from '@integration/services';
import { LinkedComponent } from '@editor/models';
import { BuilderState } from '@editor/store/state';
import * as actions from '@editor/store/actions';
import * as selectors from '@editor/store/selectors';
import * as routingSelectors from '@shared/routing/selectors';

@Component({
    selector: 'app-linked-components-library',
    templateUrl: './linked-components-library.component.html',
    styleUrls: ['./linked-components-library.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [IconComponent],
})
export class LinkedComponentsLibraryComponent implements OnInit {
    private readonly store = inject(Store<BuilderState>);
    private readonly appConfig = inject(AppConfig);

    readonly opened = signal(true);
    readonly view = toSignal(this.store.select(selectors.selectLinkedComponentsSearchView), {
        initialValue: { keyword: '', resultIds: [], results: [], totalCount: 0, loading: false, error: null },
    });
    readonly insertIndex = toSignal(this.store.select(routingSelectors.selectInsertIndexParameter), { initialValue: -1 });
    readonly canInsert = this.appConfig.getValue('canInsertLinkedComponents') === true;
    readonly canLoadMore = computed(() => {
        const view = this.view();
        return !view.loading && view.resultIds.length < view.totalCount;
    });

    ngOnInit(): void {
        if (this.canInsert) {
            this.store.dispatch(actions.searchLinkedComponents({ keyword: '' }));
        }
    }

    chooseInsertionMode(component: LinkedComponent, event: MouseEvent): void {
        event.stopPropagation();
        if (!this.canInsert) {
            return;
        }

        this.store.dispatch(actions.chooseLinkedComponentInsertionMode({
            componentId: component.id,
            insertIndex: this.insertIndex(),
            defaultMode: 'linked',
        }));
    }

    loadMore(): void {
        const view = this.view();
        if (!this.canInsert || !this.canLoadMore()) {
            return;
        }

        this.store.dispatch(actions.searchLinkedComponents({
            keyword: view.keyword,
            skip: view.resultIds.length,
        }));
    }
}
