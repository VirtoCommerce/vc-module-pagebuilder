import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NgClass } from '@angular/common';
import { ClipboardModule } from '@angular/cdk/clipboard';
import { BuilderState } from '@editor/store/state';
import { Store } from '@ngrx/store';
import { OverlapPanelComponent } from '@core/components/overlap-panel/overlap-panel.component';
import { PanelComponent } from '@core/components/panel/panel.component';
import { IconComponent } from '@core/components/icon/icon.component';
import { DynamicFormComponent } from '@core/dynamics/dynamic-form/dynamic-form.component';
import { ContextMenuComponent } from '@core/components/context-menu/context-menu.component';

import { ContextMenuAction, ModelChangedEventArgs  } from '@core/models';
import { SectionModel, SectionSchema } from '@models/document';
import { ContextMenuHelper } from '@editor/helpers';

import * as actions from '@editor/store/actions';
import * as fromState from '@editor/store/selectors';
import * as fromRoute from '@shared/routing/selectors';

@Component({
    selector: 'app-edit-section',
    templateUrl: './edit-section.component.html',
    styleUrls: ['./edit-section.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgClass, ClipboardModule, OverlapPanelComponent, PanelComponent, IconComponent, DynamicFormComponent, ContextMenuComponent]
})
export class EditSectionComponent {

    private readonly store = inject(Store<BuilderState>);
    private readonly helper = inject(ContextMenuHelper);

    readonly viewModel = toSignal(this.store.select(fromState.selectEditSectionContext));
    readonly sectionName = toSignal(this.store.select(fromState.selectCurrentItemName));
    readonly isHalfScreen = toSignal(this.store.select(fromRoute.isDesktop50), { initialValue: false });

    onBackClick() {
        this.store.dispatch(actions.closeEditItemPanel());
    }

    onModelChanged(args: ModelChangedEventArgs) {
        this.store.dispatch(actions.sectionChangedAction({ changes: args.changes }));
    }

    onContextMenuAction(action: ContextMenuAction, section: SectionModel, block: SectionModel) {
        if (action !== '|') {
            this.store.dispatch(actions.executeContextMenuAction({ action: action.action, source: 'editor', section, block }));
        }
    }

    getItemActionsFactory(item: SectionModel, schema: SectionSchema): () => Promise<ContextMenuAction[]> {
        return () => this.helper.getSectionsActions(item, !!schema.blocks?.length);
    }
}
