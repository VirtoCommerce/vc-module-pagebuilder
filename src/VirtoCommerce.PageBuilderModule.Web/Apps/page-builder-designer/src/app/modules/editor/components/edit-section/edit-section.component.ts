import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
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
import { canEditLinkedComponentOriginal, ContextMenuHelper } from '@editor/helpers';
import { AppConfig } from '@integration/services';
import { LinkedComponentUsagePage } from '@editor/models';

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
    private readonly appConfig = inject(AppConfig);

    readonly viewModel = toSignal(this.store.select(fromState.selectEditSectionContext));
    readonly linkedInstance = toSignal(this.store.select(fromState.selectLinkedComponentInstanceFromRoute));
    readonly linkedDocument = toSignal(this.store.select(fromState.selectCurrentLinkedComponent));
    readonly sectionName = toSignal(this.store.select(fromState.selectCurrentItemName));
    readonly isHalfScreen = toSignal(this.store.select(fromRoute.isDesktop50), { initialValue: false });
    readonly linkedComponentId = toSignal(this.store.select(fromRoute.selectLinkedComponentIdParameter), { initialValue: '' });
    readonly canEditLinkedComponents = canEditLinkedComponentOriginal(this.appConfig);
    readonly canInsertLinkedComponents = this.appConfig.getValue('canInsertLinkedComponents') === true;
    readonly isReadOnlyOriginal = computed(() => !!this.linkedComponentId() && !this.canEditLinkedComponents);

    onBackClick() {
        this.store.dispatch(actions.closeEditItemPanel());
    }

    onModelChanged(args: ModelChangedEventArgs) {
        if (this.isReadOnlyOriginal()) {
            return;
        }

        this.store.dispatch(actions.sectionChangedAction({ changes: args.changes }));
    }

    onContextMenuAction(action: ContextMenuAction, section: SectionModel, block: SectionModel) {
        if (!this.isReadOnlyOriginal() && action !== '|') {
            this.store.dispatch(actions.executeContextMenuAction({ action: action.action, source: 'editor', section, block }));
        }
    }

    getItemActionsFactory(item: SectionModel, schema: SectionSchema): () => Promise<ContextMenuAction[]> {
        return () => this.isReadOnlyOriginal()
            ? Promise.resolve([])
            : this.helper.getSectionsActions(item, !!schema.blocks?.length);
    }

    editLinkedComponent(): void {
        const instance = this.linkedInstance();
        if (instance && this.canEditLinkedComponents) {
            this.store.dispatch(actions.openLinkedComponent({ componentId: instance.reference.componentRef }));
        }
    }

    detachLinkedComponent(): void {
        const instance = this.linkedInstance();
        if (instance && this.canInsertLinkedComponents) {
            this.store.dispatch(actions.detachLinkedComponent({
                sectionId: instance.reference.id,
                componentId: instance.reference.componentRef,
            }));
        }
    }

    openUsagePage(page: LinkedComponentUsagePage): void {
        this.store.dispatch(actions.openLinkedComponentUsagePage({ pageId: page.id }));
    }
}
