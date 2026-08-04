import { ItemsGroup } from '@core/models';
import { SectionSchema } from '@models/document';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { KeyValuePipe } from '@angular/common';
import { Store } from '@ngrx/store';
import { OverlapPanelComponent } from '@core/components/overlap-panel/overlap-panel.component';
import { PanelComponent } from '@core/components/panel/panel.component';
import { IconComponent } from '@core/components/icon/icon.component';
import { AddSectionGroupComponent } from '@editor/controls/add-section-group/add-section-group.component';
import { AddSectionItemComponent } from '@editor/controls/add-section-item/add-section-item.component';
import { SharedComponentsLibraryComponent } from '@editor/controls/shared-components-library/shared-components-library.component';

import { BuilderState } from '@editor/store/state';

import * as actions from '@editor/store/actions';
import * as fromState from '@editor/store/selectors';
import * as fromRoute from '@shared/routing/selectors';
import { AppConfig } from '@integration/services';
import { canEditSharedComponentOriginal } from '@editor/helpers';

@Component({
    selector: 'app-add-section',
    templateUrl: './add-section.component.html',
    styleUrls: ['./add-section.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [KeyValuePipe, OverlapPanelComponent, PanelComponent, IconComponent, AddSectionGroupComponent, AddSectionItemComponent, SharedComponentsLibraryComponent]
})
export class AddSectionComponent {

    private readonly store = inject(Store<BuilderState>);
    private readonly appConfig = inject(AppConfig);

    readonly title = toSignal(this.store.select(fromState.selectAddItemTitle));
    readonly viewModel = toSignal(this.store.select(fromState.selectAddItemContext));
    readonly filter = toSignal(this.store.select(fromState.selectCurrentSectionsFilter));
    readonly isHalfScreen = toSignal(this.store.select(fromRoute.isDesktop50), { initialValue: false });
    readonly isReadOnly = computed(() =>
        !!this.viewModel()?.isSharedComponentDocument && !canEditSharedComponentOriginal(this.appConfig));

    onCancelClick() {
        this.store.dispatch(actions.closeAddItemPanel());
    }

    onToggleGroup(group: ItemsGroup<SectionSchema>) {
        this.store.dispatch(actions.toggleGroupAction({ groupId: group.name }));
    }

    onPreviewItem(item: SectionSchema) {
        if (this.isReadOnly()) {
            return;
        }
        this.store.dispatch(actions.previewItemAction({ item }))
    }

    onAddItem(schema: SectionSchema) {
        if (this.isReadOnly()) {
            return;
        }
        this.store.dispatch(actions.addItemAction({ schema }));
    }

    applySectionsFilter(event: Event) {
        const value = (event.target as HTMLInputElement).value;
        this.store.dispatch(actions.applySectionsFilter({ filter: value }));
        this.store.dispatch(actions.searchSharedComponents({ keyword: value }));
    }
}
