import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { tap, map } from 'rxjs/operators';
import { PlatformService, ApiUrlsService } from '@app/services';
import { BlocksSchema, BlockSchema, SharedBlockSchema, ControlDescriptor } from '@shared/models';

@Injectable({
    providedIn: 'root'
})
export class BlocksService {
    constructor(private platform: PlatformService, private urls: ApiUrlsService) { }

    load(): Observable<BlocksSchema> {
        return this.platform.downloadBlocksSchema().pipe(
            tap(schema => {
                const shared = <SharedBlockSchema>schema.shared;
                Object.keys(schema).forEach(key => {
                    const contentType = schema[key].contentType;
                    if (!contentType || (typeof contentType === 'string' && contentType === this.urls.params.contentType) ||
                        (Array.isArray(contentType) && (<string[]>contentType).some(x => x === this.urls.params.contentType))) {
                        const currentBlock = schema[key];
                        currentBlock.type = key;
                        if (!!shared) {
                            this.mergeSettings(currentBlock, shared);
                        }
                    } else {
                        delete schema[key];
                    }
                });
            })
        );
    }

    private mergeSettings(block: BlockSchema, shared: SharedBlockSchema) {
        const blockSettings = block.settings;
        // finction to add setting to block settings collection
        const addSetting = (sharedSettings: ControlDescriptor[], excludePredicate: (string) => boolean = () => false) => {
            if (!sharedSettings) return;
            sharedSettings.forEach(item => {
                if (!excludePredicate(item.id)) {
                    const settingIndex = blockSettings.findIndex(b => b.id === item.id);
                    if (settingIndex === -1) {
                        blockSettings.push(item);
                    } else {
                        // some of properties can be overriden
                        const overrides = blockSettings[settingIndex];
                        blockSettings[settingIndex] = { ...item, ...overrides };
                    }
                }
            });
        };
        // block.includeShared contains name or array of names named settings in shared block
        if (block.includeShared && shared.namedSettings) {
            if (typeof block.includeShared === 'string') {
                const sharedSettings = shared.namedSettings[block.includeShared];
                addSetting(sharedSettings);
            } else {
                block.includeShared.forEach(name => {
                    const sharedSettings = shared.namedSettings[name];
                    addSetting(sharedSettings);
                });
            }
        }
        // block can skip shared settings or some of them
        const skipShared = block.excludeShared;
        if (!skipShared || Array.isArray(skipShared)) {
            const exclude = Array.isArray(skipShared) ? (v: string) => (<string[]>skipShared).indexOf(v) !== -1 : () => false;
            const sharedSettings = shared.settings;
            addSetting(sharedSettings, exclude);
        }
    }
}
