import { CdkDragSortEvent, CdkDragStart, DragDropModule } from '@angular/cdk/drag-drop';
import { afterNextRender, ChangeDetectionStrategy, Component, computed, ElementRef, inject, Injector, input, output, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { BlockStatesList, SectionsSchemasList } from '@editor/models';
import { ReorderItemsModel } from '@core/models';
import { SectionModel } from '@models/document';
import { domHelpers } from '@core/helpers';
import { getKeyboardReorderIndices } from '@editor/helpers/editor.helpers';
import { SectionItemComponent } from '@editor/controls/section-item/section-item.component';
import { DragHandleComponent } from '@core/components/drag-handle/drag-handle.component';
import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';

@Component({
    selector: 'app-section-children-list',
    templateUrl: './section-children-list.component.html',
    styleUrls: ['./section-children-list.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgClass, DragDropModule, SectionItemComponent, DragHandleComponent, IconButtonComponent]
})
export class SectionChildrenListComponent {
    private readonly elementRef = inject<ElementRef<HTMLElement>>(ElementRef);
    private readonly injector = inject(Injector);

    private _fakeElement: HTMLElement | null = null;

    readonly currentHoverId = signal<string | null>(null);
    readonly moveAnnouncement = signal('');

    readonly section = input.required<SectionModel>();
    readonly blocksSchemas = input.required<SectionsSchemasList>();
    readonly states = input.required<BlockStatesList>();
    readonly selectedBlocksCount = computed(() =>
        Object.values(this.states() || {}).filter(x => x.selected).length
    );
    readonly selectMode = input(false);
    readonly readOnly = input(false);

    readonly itemClick = output<SectionModel>();
    readonly checkChanged = output<{ blockId: string, selected: boolean }>();
    readonly addBlockClick = output();
    readonly reorderBlocks = output<ReorderItemsModel>();
    readonly executeAction = output<{ action: string, block: SectionModel }>();

    onReorderBlocks(event: CdkDragSortEvent<SectionModel>) {
        if (this.readOnly()) {
            return;
        }
        this.reorderBlocks.emit({ item: event.item.data, currentIndex: event.currentIndex, previousIndex: event.previousIndex, parent: this.section() });
    }

    moveBlock(previousIndex: number, offset: number) {
        const blocks = this.section().blocks || [];
        const selectedIndexes = blocks.map((block, index) => this.states()[block.id]?.selected ? index : -1)
            .filter(index => index >= 0);
        const indices = getKeyboardReorderIndices(blocks.length, selectedIndexes, previousIndex, offset as -1 | 1);
        if (this.readOnly() || !indices) {
            return;
        }
        const movedId = blocks[previousIndex].id;
        const document = this.elementRef.nativeElement.ownerDocument;
        const focusedHandle = document.activeElement as HTMLElement | null;
        const shouldRestoreFocus = focusedHandle?.closest('[data-block-id]')?.getAttribute('data-block-id') === movedId;
        const { currentIndex } = indices;
        this.reorderBlocks.emit({ item: blocks[indices.previousIndex], ...indices, parent: this.section() });
        const direction = offset < 0 ? 'up' : 'down';
        const announcement = selectedIndexes.length > 1
            ? `Selected blocks moved ${direction}`
            : `Block moved to position ${currentIndex + 1} of ${blocks.length}`;
        this.moveAnnouncement.set('');
        afterNextRender(() => {
            if (shouldRestoreFocus && (document.activeElement === document.body || document.activeElement === focusedHandle)) {
                const handle = Array.from(this.elementRef.nativeElement.querySelectorAll<HTMLElement>('[data-block-id]'))
                    .find(element => element.dataset['blockId'] === movedId)
                    ?.querySelector<HTMLButtonElement>('button');
                handle?.focus({ preventScroll: true });
            }
            this.moveAnnouncement.set(announcement);
        }, { injector: this.injector });
    }

    onItemClick(block: SectionModel) {
        this.itemClick.emit(block);
    }

    onAddBlockClick() {
        if (this.readOnly()) {
            return;
        }
        this.addBlockClick.emit();
    }

    onActionExecuted(action: string, block: SectionModel) {
        if (this.readOnly()) {
            return;
        }
        this.executeAction.emit({ action, block });
    }

    onItemSelectChanged(selected: boolean, blockId: string) {
        if (this.readOnly()) {
            return;
        }
        this.checkChanged.emit({ blockId, selected });
    }

    blockDragStarted(event: CdkDragStart) {
        if (this.readOnly()) {
            return;
        }
        const rootElement = event.source.getRootElement();
        this._fakeElement = domHelpers.deepCloneNode(rootElement);
        domHelpers.toggleVisibility(this._fakeElement, true, new Set(['position']));
        this._fakeElement.classList.add('dragging');
        event.source.dropContainer.element.nativeElement.insertBefore(this._fakeElement, event.source.getPlaceholderElement());
    }

    blockDragCompleted() {
        this._fakeElement?.remove();
        this._fakeElement = null;
    }
}
