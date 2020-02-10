import { Component, OnInit } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { Subscription, of } from 'rxjs';
import { Store } from '@ngrx/store';

import { CdkDragSortEvent } from '@angular/cdk/drag-drop';

import * as fromRoot from '@app/store';
import * as rootActions from '@app/store/root.actions';

import * as fromEditor from '@editor/store';
import * as editorActions from '@editor/store/editor.actions';

import * as fromTheme from '@themes/store';
import * as themeActions from '@themes/store/theme.actions';

import { BlockValuesModel, BlockSchema } from '@shared/models';
import { map } from 'rxjs/operators';
import { environment } from 'src/environments/environment';

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    styleUrls: ['./app.component.css']
})
export class AppComponent implements OnInit {
    title = 'cms-designer';
    viewMode = 'desktop';

    themeActions = [
        { title: 'Edit HTML/CSS', icon: 'html', type: 'html' },
        { title: 'Edit languages', icon: 'lang', type: 'lang' },
        { title: 'Edit navigation', icon: 'nav', type: 'nav' }
    ];

    version = environment.version;

    storeUrl$ = this.store.select(fromRoot.getPreviewUrl).pipe(
        map(x => !!x ? this.sanitizer.bypassSecurityTrustResourceUrl(x) : null)
    );

    // page editor states
    currentSectionItem$ = this.store.select(fromEditor.getCurrentSectionItem);
    addNewSectionMode$ = this.store.select(fromEditor.getAddNewSectionMode);
    blocksSchema$ = this.store.select(fromEditor.getBlocksSchema);
    page$ = this.store.select(fromEditor.getPageForEdit);
    schemaNotLoaded$ = this.store.select(fromEditor.getSchemaNotLoaded);
    pageNotLoaded$ = this.store.select(fromEditor.getPageNotLoaded);
    hoveredId$ = this.store.select(fromEditor.getHoveredId);
    currentBlockName$ = this.store.select(fromEditor.getCurrentBlockName);
    editorLoading$ = this.store.select(fromEditor.getIsLoading);
    editorMode$ = this.store.select(fromEditor.getEditorMode);

    // theme editor states
    presets$ = this.store.select(fromTheme.getPresets);
    themeSchema$ = this.store.select(fromTheme.getSchema);
    theme$ = this.store.select(fromTheme.getEditableTheme);
    currentThemeSchemaItem$ = this.store.select(fromTheme.getCurrentThemeSchemaItem);
    showPresets$ = this.store.select(fromTheme.getShowPresetsEditor);
    presetsNotLoaded$ = this.store.select(fromTheme.getPresetsNotLoaded);
    themeSchemaNotLoaded$ = this.store.select(fromTheme.getSchemaNotLoaded);

    // combined & common states
    primaryId$ = this.store.select(fromRoot.getPrimaryFrameId);
    headerActive$ = this.store.select(fromRoot.getHeaderIsActive);
    headerIcon$ = this.store.select(fromRoot.getHeaderIcon);
    tabsVisible$ = this.store.select(fromRoot.getIsTabVisible);
    isLoading$ = this.store.select(fromRoot.getIsLoading);
    isEditMode$ = this.store.select(fromRoot.getIsEditMode);
    isDirty$ = this.store.select(fromRoot.getIsDirty);
    previewLoading$ = this.store.select(fromRoot.getPreviewLoading);
    previewError$ = this.store.select(fromRoot.getPreviewError);
    activeTabIndex$ = this.store.select(fromRoot.getActiveTabIndex);
    pageTitle$ = this.store.select(fromEditor.getPageTitle);
    hasUndo$ = of(true);
    hasRedo$ = of(false);

    constructor(private store: Store<fromRoot.State>,
        private sanitizer: DomSanitizer) { }

    ngOnInit(): void {
        this.store.dispatch(rootActions.loadData());
    }

    onPreviewLoaded(source: string) {
        this.store.dispatch(rootActions.previewReady({ frameId: source }));
    }

    onPreviewError(error) {
        this.store.dispatch(rootActions.previewError({ error }));
    }

    reloadPreview() {
        this.store.dispatch(rootActions.reloadPreview());
    }

    back() {
        this.store.dispatch(rootActions.closeEditors());
    }

    // onThemeActionSelected(type: string) {
    //     // TODO:
    //     console.log(type);
    // }

    onTabChanged(tabIndex) {
        this.store.dispatch(rootActions.tabIndexChanged({ tabIndex }));
    }

    onSave() {
        this.store.dispatch(rootActions.saveData());
    }

    // undo() {
    //     console.log('undo');
    // }

    // redo() {
    //     console.log('redo');
    // }

    // editor tab events

    mouseOverItem(block) {
        this.store.dispatch(editorActions.highlightInPreview({ block }));
    }

    selectPageItem(item: BlockValuesModel) {
        this.store.dispatch(editorActions.selectPageItem({ blockId: item.id }));
    }

    reloadEditorData() {
        this.store.dispatch(rootActions.loadData());
    }

    onOrderChanged(event: CdkDragSortEvent<BlockValuesModel>) {
        this.store.dispatch(editorActions.swapBlocks(event));
        this.store.dispatch(editorActions.orderChanged(event));
    }

    // block editor pane events

    updateBlockPreview(block: BlockValuesModel) {
        this.store.dispatch(editorActions.updatePageItem({ block }));
        this.store.dispatch(editorActions.updateBlockPreview({ block }));
    }

    changeEditorMode(mode: string) {
        this.store.dispatch(editorActions.setEditorMode({ mode }));
    }

    removeBlock(block: BlockValuesModel) {
        this.store.dispatch(editorActions.removePageItem({ block }));
    }

    copyBlock(sourceBlock: BlockValuesModel) {
        this.store.dispatch(editorActions.copyPageItem({ sourceBlock }));
    }

    toggleVisibility(block: BlockValuesModel) {
        this.store.dispatch(editorActions.toggleItemVisibility({ block }));
    }

    // add new block pane events

    setNewBlockMode() {
        this.store.dispatch(editorActions.toggleNewBlockPane({ display: true }));
    }

    previewBlockType(blockSchema: BlockSchema) {
        this.store.dispatch(editorActions.previewPageItemOfType({ blockSchema }));
    }

    selectBlockType(newItemSchema: BlockSchema) {
        this.store.dispatch(editorActions.createPageItem({ newItemSchema }));
    }

    // theme tab events

    turnOnPresets() {
        this.store.dispatch(themeActions.showPresetsPane());
    }

    reloadThemeData() {
        this.store.dispatch(rootActions.loadData());
    }

    selectSchemaItem(item: BlockSchema) {
        this.store.dispatch(themeActions.selectSchemaItem({ item }));
    }

    // presets pane events

    onSavePreset(preset: string) {
        this.store.dispatch(themeActions.createPreset({ preset }));
    }

    onSelectPreset(preset: string) {
        this.store.dispatch(themeActions.selectPreset({ preset }));
    }

    applyPresetAsTheme(preset: string) {
        this.store.dispatch(themeActions.applyPreset({ preset }));
    }

    onRemovePreset(preset: string) {
        this.store.dispatch(themeActions.removePreset({ preset }));
    }

    // theme editor pane events

    liveUpdateTheme(values: { [key: string]: string | number | boolean }) {
        this.store.dispatch(themeActions.updateTheme({ values }));
    }
}
