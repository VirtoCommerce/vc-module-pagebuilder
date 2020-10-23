import { Component, OnInit, Input, Output, EventEmitter, AfterContentInit, OnDestroy } from '@angular/core';
import { BlockSchema, BlockValuesModel, BlocksSchema, ComponentContext } from '@shared/models';
import { Subject, combineLatest, Observable, fromEvent, Subscription } from 'rxjs';
import { map, withLatestFrom } from 'rxjs/operators';
import { WindowRef } from '@app/services';

@Component({
    selector: 'app-page-item-editor',
    templateUrl: './page-item-editor.component.html',
    styleUrls: ['./page-item-editor.component.scss']
})
export class PageItemEditorComponent implements OnInit {

    private _mode: string;

    @Input() model: BlockValuesModel;
    @Input() schema: BlocksSchema;
    @Input() blockName: string;
    @Input() tabs: string[];
    @Input() activeTab: string;
    @Input() context: ComponentContext;
    @Input() get mode(): string {
        return this._mode;
    }
    set mode(value: string) {
        this._mode = value;
        this.adjustPanelWidth();
    }

    @Output() backEvent = new EventEmitter<BlockValuesModel>();
    @Output() valueChangedEvent = new EventEmitter<BlockValuesModel>();
    @Output() removeBlockEvent = new EventEmitter<BlockValuesModel>();
    @Output() copyBlockEvent = new EventEmitter<BlockValuesModel>();
    @Output() copyToClipboardEvent = new EventEmitter<BlockValuesModel>();
    @Output() changeEditorModeEvent = new EventEmitter<string>();
    @Output() changeCurrentTabEvent = new EventEmitter<string>();

    openedWidthStyle: number;

    private editedModel: BlockValuesModel;

    constructor(private windowRef: WindowRef) { }

    ngOnInit() {
        this.windowRef.nativeWindow.addEventListener('resize', () => this.adjustPanelWidth());
    }

    togglePanel() {
        this.changeEditorModeEvent.emit(this.mode === 'wide' ? 'normal' : 'wide');
    }

    setActiveTab(tabName: string) {
        this.changeCurrentTabEvent.emit(tabName);
    }

    modelChanged(model) {
        this.editedModel = (!this.schema.static) ? model : {
            ...model,
            type: 'settings'
        };
        this.valueChangedEvent.emit(this.editedModel);
    }

    removeBlock() {
        this.removeBlockEvent.emit(this.model);
    }

    copyBlock() {
        this.copyBlockEvent.emit(this.model);
    }

    copyToClipboard() {
        this.copyToClipboardEvent.emit(this.model);
    }

    private adjustPanelWidth() {
        this.openedWidthStyle = this.mode === 'wide' ? this.windowRef.nativeWindow.innerWidth / 2 : null;
    }
}
