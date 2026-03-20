import { Component, ElementRef, signal, viewChild, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NgClass, NgStyle } from '@angular/common';
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

import { ContextMenuHelper } from '@editor/helpers';
import { BuilderState } from '@editor/store/state';

import * as fromState from '@editor/store/selectors';
import * as actions from '@editor/store/actions';
import { BlockState } from '../../models';
import { domHelpers } from '@core/helpers';

@Component({
    selector: 'app-template-editor',
    templateUrl: './template-editor.component.html',
    styleUrls: ['./template-editor.component.scss'],
    imports: [NgClass, NgStyle, DragDropModule, PanelComponent, CollapsibleListItemComponent, IconComponent, IconButtonComponent, ContextMenuComponent, DragHandleComponent, SectionItemComponent, SectionChildrenListComponent]
})
export class TemplateEditorComponent {

    private readonly store = inject(Store<BuilderState>);
    private readonly helper = inject(ContextMenuHelper);

    readonly container = viewChild.required<ElementRef<HTMLDivElement>>('container');

    readonly viewModel = toSignal(this.store.select(fromState.editTemplateContext));

    readonly hoveredSectionId = toSignal(this.store.select(fromState.hoveredSectionId));
    readonly templateName = toSignal(this.store.select(fromState.selectCurrentTemplateName));

    readonly addButtonTop = signal('0');
    readonly addLineTop = signal('0');
    readonly addButtonOpacity = signal(0);
    readonly currentInsertIndex = signal(0);
    readonly currentHoverId = signal<string | null>(null);

    addSectionClick() {
        this.store.dispatch(actions.showBlankSections({ sectionId: null, positionIndex: this.currentInsertIndex() }));
    }

    onSettingsClick(schema: SectionSchema) {
        this.store.dispatch(actions.editSettings({ schema }))
    }

    private _fakeElement: HTMLElement | null = null;

    reorderSections(event: CdkDragSortEvent<SectionModel>) {
        this.store.dispatch(actions.sortItems({ options: { item: event.item.data, currentIndex: event.currentIndex, previousIndex: event.previousIndex } }))
    }
    sectionDragStarted(event: CdkDragStart, section: SectionModel) {
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

        this.store.dispatch(actions.releaseDragSection({ sectionId: section.id }));
    }

    reorderBlocks(options: ReorderItemsModel) {
        this.store.dispatch(actions.sortItems({ options }))
    }

    onSectionClick(section: SectionModel) {
        this.store.dispatch(actions.editSectionAction({ sectionId: section.id }));
    }

    onSectionHover(section: SectionModel) {
        this.store.dispatch(actions.hoverSection({ sectionId: section.id }));
    }

    onItemSelectChanged(selected: boolean, section: SectionModel, templateKey: string) {
        this.store.dispatch(actions.sectionStateChangedAction({ sectionId: section.id, templateKey, state: { selected } }));
    }

    onBlockSelectChanged(selected: boolean, blockId: string, sectionId: string, templateKey: string) {
        this.store.dispatch(actions.sectionStateChangedAction({ sectionId, templateKey, state: { blocks: { [blockId]: <BlockState>{ selected } } } }));
    }

    onBlockClick(section: SectionModel, block: SectionModel) {
        this.store.dispatch(actions.editBlockAction({ sectionId: section.id, blockId: block.id }));
    }
    addBlockClick(section: SectionModel) {
        this.store.dispatch(actions.showBlankSections({ sectionId: section.id, positionIndex: -1 }));
    }

    toggleSection(expanded: boolean, sectionId: string, templateKey: string) {
        this.store.dispatch(actions.sectionStateChangedAction({ sectionId, templateKey, state: { expanded } }));
    }

    onActionClick(event: string, section?: SectionModel, block?: SectionModel) {
        this.store.dispatch(actions.executeContextMenuAction({ action: event, source: 'list', section, block }));
    }

    readonly getPageActions = () => this.helper.getPageActions();

    onMouseMove(args: MouseEvent) {
        let target = this.container().nativeElement;
        const rect = target.getBoundingClientRect();
        const top = args.clientY - rect.top;

        const w2 = rect.width / 2.0;
        this.addButtonOpacity.set(1 - Math.abs(w2 - args.clientX - rect.left) / w2);

        if (top < 0) {
            this.currentInsertIndex.set(0);
            this.addButtonTop.set('-18px');
            this.addLineTop.set('-6px');
            return;
        }

        for (let i = 0; i < target.children.length; i++) {
            const childRect = target.children[i].getBoundingClientRect();
            const childTop = childRect.top - rect.top;
            const childBottom = childRect.bottom - rect.top;
            if (top >= childTop && top < childBottom + 10) {
                this.currentInsertIndex.set(i + 1);
                const position = childBottom - 14;
                this.addButtonTop.set(`${position}px`);
                this.addLineTop.set(`${position + 12}px`);
                return;
            }
        }

        this.currentInsertIndex.set(target.children.length);
        this.addButtonTop.set(`${rect.height - 14}px`);
        this.addLineTop.set(`${rect.height - 2}px`);
    }

    onMouseLeave() {
        this.addButtonOpacity.set(0);
        this.currentInsertIndex.set(this.container().nativeElement.children.length);
    }
}
