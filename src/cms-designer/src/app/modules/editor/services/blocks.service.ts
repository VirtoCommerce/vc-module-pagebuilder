import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { tap, map } from 'rxjs/operators';
import { PlatformService, ApiUrlsService, AppSettings } from '@app/services';
import { BlocksSchema, BlockSchema, SharedBlockSchema, ControlDescriptor } from '@shared/models';

@Injectable({
    providedIn: 'root'
})
export class BlocksService {
    constructor(private platform: PlatformService, private appSettings: AppSettings) { }

    load(): Observable<BlocksSchema> {
        return this.platform.downloadBlocksSchema().pipe(
            tap(schema => {
                const shared = <SharedBlockSchema>schema.shared;
                Object.keys(schema).forEach(key => {
                    const contentType = schema[key].contentType;
                    if (!contentType || (typeof contentType === 'string' && contentType === this.appSettings.contentType) ||
                        (Array.isArray(contentType) && (<string[]>contentType).some(x => x === this.appSettings.contentType))) {
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

        if (!block.static && shared) {

            // 1. take all included shared settings
            const sharedSettings = block.excludeShared === true ? [] : (!shared.settings ? [] : [...shared.settings]);

            // 2. add included named shared
            if (!!block.includeShared && shared.namedSettings) {
                const sharedNames = typeof block.includeShared === 'string' ? [block.includeShared] : block.includeShared;
                const sharedNamesSettings = sharedNames.filter(x => !!shared.namedSettings[x]).reduce((acc, name) => ([...acc, ...shared.namedSettings[name]]), []);
                sharedSettings.push(...sharedNamesSettings);
            }

            // 3. remove items from excluded list
            if (Array.isArray(block.excludeShared)) {
                block.excludeShared.forEach(id => {
                    const index = sharedSettings.findIndex(b => b.id === id);
                    if (index !== -1) {
                        sharedSettings.splice(index, 1);
                    }
                });
            }

            // 4. override shared settings by block settings
            if (!block.settings) {
                block.settings = [];
            }
            const blockSettings = block.settings.map(x => {
                const settings = sharedSettings.find(shared => shared.id == x.id);
                if (!!settings) {
                    return { ...settings, ...x };
                } else {
                    return x;
                }
            });

            // 5. apply items
            const settings = [...blockSettings, ...sharedSettings.filter(x => !blockSettings.find(b => b.id === x.id))];
            block.settings = settings;
        }

        // 6. order items
        block.settings.sort((x, y) => (x.sort || 0) - (y.sort || 0));
    }
}
