import { afterNextRender, Component, computed, ElementRef, signal, inject, Injector } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { LiveAnnouncer } from '@angular/cdk/a11y';
import { NgClass } from '@angular/common';
import { CdkDragRelease, CdkDragSortEvent, CdkDragStart, DragDropModule } from '@angular/cdk/drag-drop';
import { Store } from '@ngrx/store';
import { PanelComponent } from '@core/components/panel/panel.component';
import { CollapsibleListItemComponent } from '@core/components/collapsible-list-item/collapsible-list-item.component';
import { IconComponent } from '@core/components/icon/icon.component';
import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';
import { ContextMenuComponent } from '@core/components/context-menu/context-menu.component';
import { DragHandleComponent } from '@core/components/drag-handle/drag-handle.component';
import { SectionItemComponent } from '@editor/controls/section-item/section-item.component';
import { SectionChildrenListComponent } from '@editor/controls/section-children-list/section-children-list.component';

import { ReorderItemsModel } from '@core/models';
import { SectionModel, SectionSchema } from '@models/document';

import {
    canEditSharedComponentOriginal,
    canOpenSharedComponentUsagePage,
    ContextMenuHelper,
    isSharedComponentReference,
} from '@editor/helpers';
import { AppConfig } from '@integration/services';
import { SharedComponent, SharedComponentUsagePage } from '@editor/models';
import { BuilderState } from '@editor/store/state';

import * as fromState from '@editor/store/selectors';
import * as actions from '@editor/store/actions';
import * as routingSelectors from '@shared/routing/selectors';
import { BlockState } from '../../models';
import { domHelpers } from '@core/helpers';
import { getKeyboardReorderIndices, getSectionName } from '@editor/helpers/editor.helpers';

@Component({
    selector: 'app-template-editor',
    templateUrl: './template-editor.component.html',
    styleUrls: ['./template-editor.component.scss'],
    imports: [NgClass, DragDropModule, PanelComponent, CollapsibleListItemComponent, IconComponent, IconButtonComponent, ContextMenuComponent, DragHandleComponent, SectionItemComponent, SectionChildrenListComponent]
})
export class TemplateEditorComponent {

    private readonly store = inject(Store<BuilderState>);
    private readonly helper = inject(ContextMenuHelper);
    private readonly appConfig = inject(AppConfig);
    private readonly elementRef = inject<ElementRef<HTMLElement>>(ElementRef);
    private readonly injector = inject(Injector);
    private readonly liveAnnouncer = inject(LiveAnnouncer);

    readonly viewModel = toSignal(this.store.select(fromState.editTemplateContext));
    readonly loadState = toSignal(this.store.select(fromState.selectCurrentTemplateState));
    readonly sharedComponentId = toSignal(
        this.store.select(routingSelectors.selectSharedComponentIdParameter),
        { initialValue: '' },
    );

    readonly hoveredSectionId = toSignal(this.store.select(fromState.hoveredSectionId));
    readonly templateName = toSignal(this.store.select(fromState.selectCurrentTemplateName));
    readonly canEditSharedComponents = canEditSharedComponentOriginal(this.appConfig);
    readonly isSharedComponentDocument = computed(() => !!this.sharedComponentId());
    readonly isReadOnly = computed(() => this.isSharedComponentDocument() && !this.canEditSharedComponents);

    canMutate(): boolean {
        return !this.isReadOnly();
    }

    getSectionLabel(section: SectionModel, schema?: SectionSchema): string {
        return getSectionName(section, schema ?? null);
    }

    readonly currentHoverId = signal<string | null>(null);

    addSectionClick(positionIndex?: number) {
        if (!this.canMutate()) {
            return;
        }
        this.store.dispatch(actions.showBlankSections({
            sectionId: null,
            positionIndex: positionIndex ?? this.viewModel()?.template?.content.length ?? 0,
        }));
    }

    onSettingsClick(schema: SectionSchema) {
        if (!this.canMutate()) {
            return;
        }
        this.store.dispatch(actions.editSettings({ schema }))
    }

    private _fakeElement: HTMLElement | null = null;

    reorderSections(event: CdkDragSortEvent<SectionModel>) {
        if (!this.canMutate()) {
            return;
        }
        this.store.dispatch(actions.sortItems({ options: { item: event.item.data, currentIndex: event.currentIndex, previousIndex: event.previousIndex } }))
    }
    sectionDragStarted(event: CdkDragStart, section: SectionModel) {
        if (!this.canMutate()) {
            return;
        }
        const rootElement = event.source.getRootElement();
        this._fakeElement = domHelpers.deepCloneNode(rootElement);
        domHelpers.toggleVisibility(this._fakeElement, true, new Set(['position']));
        this._fakeElement.classList.add('dragging');
        event.source.dropContainer.element.nativeElement.insertBefore(this._fakeElement, event.source.getPlaceholderElement());

        this.store.dispatch(actions.startDragSection({ sectionId: section.id }));
    }
    sectionDragCompleted(_event: CdkDragRelease, section: SectionModel) {
        this._fakeElement?.remove();
        this._fakeElement = null;

        if (!this.canMutate()) {
            return;
        }
        this.store.dispatch(actions.releaseDragSection({ sectionId: section.id }));
    }

    reorderBlocks(options: ReorderItemsModel) {
        if (!this.canMutate()) {
            return;
        }
        this.store.dispatch(actions.sortItems({ options }))
    }

    onSectionClick(section: SectionModel) {
        this.store.dispatch(actions.editSectionAction({ sectionId: section.id }));
    }

    onSectionHover(section: SectionModel | null) {
        this.store.dispatch(actions.hoverSection({ sectionId: section?.id || null }));
    }

    onItemSelectChanged(selected: boolean, section: SectionModel, templateKey: string) {
        if (!this.canMutate()) {
            return;
        }
        this.store.dispatch(actions.sectionStateChangedAction({ sectionId: section.id, templateKey, state: { selected } }));
    }

    onBlockSelectChanged(selected: boolean, blockId: string, sectionId: string, templateKey: string) {
        if (!this.canMutate()) {
            return;
        }
        this.store.dispatch(actions.sectionStateChangedAction({ sectionId, templateKey, state: { blocks: { [blockId]: <BlockState>{ selected } } } }));
    }

    onBlockClick(section: SectionModel, block: SectionModel) {
        this.store.dispatch(actions.editBlockAction({ sectionId: section.id, blockId: block.id }));
    }
    addBlockClick(section: SectionModel) {
        if (!this.canMutate()) {
            return;
        }
        this.store.dispatch(actions.showBlankSections({ sectionId: section.id, positionIndex: -1 }));
    }

    toggleSection(expanded: boolean, sectionId: string, templateKey: string) {
        this.store.dispatch(actions.sectionStateChangedAction({ sectionId, templateKey, state: { expanded } }));
    }

    onActionClick(event: string, section?: SectionModel, block?: SectionModel) {
        if (!this.canMutate()) {
            return;
        }
        this.store.dispatch(actions.executeContextMenuAction({ action: event, source: 'list', section, block }));
    }

    readonly getPageActions = () => this.helper.getPageActions(
        !!this.viewModel()?.selectMode,
        (this.viewModel()?.selectedSectionsCount || 0) > 0,
        !this.viewModel()?.isSharedComponentDocument,
    );

    backToPage(): void {
        this.store.dispatch(actions.closeSharedComponent());
    }

    retrySharedComponentLoad(): void {
        const templateKey = this.loadState()?.key;
        if (templateKey) {
            this.store.dispatch(actions.loadTemplateModel({ templateKey }));
        }
    }

    openUsagePage(page: SharedComponentUsagePage): void {
        if (canOpenSharedComponentUsagePage(page)) {
            this.store.dispatch(actions.openSharedComponentUsagePage({
                pageId: page.id,
                cultureName: page.cultureName,
            }));
        }
    }

    canOpenUsagePage(page: SharedComponentUsagePage): boolean {
        return canOpenSharedComponentUsagePage(page);
    }

    getSharedComponent(section: SectionModel): SharedComponent | null {
        if (!isSharedComponentReference(section)) {
            return null;
        }
        return this.viewModel()?.sharedComponents[section.componentRef] || null;
    }

    getSharedComponentError(section: SectionModel): string | null {
        if (!isSharedComponentReference(section)) {
            return null;
        }
        return this.viewModel()?.sharedComponentErrors[section.componentRef] || null;
    }

    moveSection(previousIndex: number, offset: number) {
        const vm = this.viewModel();
        const content = vm?.template?.content || [];
        const selectedIndexes = content.map((section, index) => vm?.sectionsState[section.id]?.selected ? index : -1)
            .filter(index => index >= 0);
        const indices = getKeyboardReorderIndices(content.length, selectedIndexes, previousIndex, offset as -1 | 1);
        if (!this.canMutate() || !indices) {
            return;
        }
        const movedId = content[previousIndex].id;
        const document = this.elementRef.nativeElement.ownerDocument;
        const focusedHandle = document.activeElement as HTMLElement | null;
        const shouldRestoreFocus = focusedHandle?.closest('[data-section-id]')?.getAttribute('data-section-id') === movedId;
        const { currentIndex } = indices;
        this.store.dispatch(actions.sortItems({ options: { item: content[indices.previousIndex], ...indices } }));
        const direction = offset < 0 ? 'up' : 'down';
        const announcement = selectedIndexes.length > 1
            ? `Selected sections moved ${direction}`
            : `Section moved to position ${currentIndex + 1} of ${content.length}`;
        void this.liveAnnouncer.announce(announcement);
        afterNextRender(() => {
            if (shouldRestoreFocus && (document.activeElement === document.body || document.activeElement === focusedHandle)) {
                const handle = Array.from(this.elementRef.nativeElement.querySelectorAll<HTMLElement>('[data-section-id]'))
                    .find(element => element.dataset['sectionId'] === movedId)
                    ?.querySelector<HTMLButtonElement>('button');
                handle?.focus({ preventScroll: true });
            }
        }, { injector: this.injector });
    }
}
