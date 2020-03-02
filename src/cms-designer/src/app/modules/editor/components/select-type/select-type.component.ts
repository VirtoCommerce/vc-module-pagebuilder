import { Component, OnInit, Output, EventEmitter, Input } from '@angular/core';
import { BlockSchema } from '@shared/models';
import { CreateBlockModel } from '@editor/models';

@Component({
    selector: 'app-select-type',
    templateUrl: './select-type.component.html',
    styleUrls: ['./select-type.component.scss']
})
export class SelectTypeComponent implements OnInit {

    @Input() types: CreateBlockModel;
    @Output() previewBlockEvent = new EventEmitter<BlockSchema>();
    @Output() selectBlockEvent = new EventEmitter<BlockSchema>();

    selectedGroup: string;
    selectedItem: string;

    constructor() { }

    ngOnInit() { }

    previewItem(item: BlockSchema) {
        this.selectedItem = item.type;
        this.previewBlockEvent.emit(item);
    }

    toggleGroup(group: { name: string, items: [] }) {
        this.selectedGroup = this.selectedGroup === group.name ? null : group.name;
    }

    selectItem(item: BlockSchema) {
        this.selectBlockEvent.emit(item);
    }
}
