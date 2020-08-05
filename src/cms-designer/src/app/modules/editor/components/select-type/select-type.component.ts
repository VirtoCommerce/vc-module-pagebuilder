import { Component, OnInit, Output, EventEmitter, Input } from '@angular/core';
import { BlockSchema } from '@shared/models';
import { CreateBlockModel } from '@editor/models';

@Component({
    selector: 'app-select-type',
    templateUrl: './select-type.component.html',
    styleUrls: ['./select-type.component.scss']
})
export class SelectTypeComponent implements OnInit {

    private allItems: BlockSchema[] = [];

    @Input() types: CreateBlockModel;
    @Output() previewBlockEvent = new EventEmitter<BlockSchema>();
    @Output() selectBlockEvent = new EventEmitter<BlockSchema>();

    selectedGroup: string;
    selectedItem: string;

    searchQuery: string;
    filteredItems: BlockSchema[] = [];

    constructor() { }

    ngOnInit() {
        this.allItems = this.types.groups.reduce((acc, group) => [...acc, ...group.items], []);
    }

    onSearch() {
        const q = this.searchQuery.toLowerCase();
        if (this.searchQuery) {
            this.filteredItems = this.allItems.filter(x => x.name.toLowerCase().indexOf(q) !== -1 || x.type.indexOf(q) !== -1)
        } else {
            this.filteredItems = [];
        }        
    }

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
