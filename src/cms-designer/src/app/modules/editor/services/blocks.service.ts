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

        if (block.static || !shared)
            return;

        // 1. take all included shared settings
        const sharedSettings = block.excludeShared === true ? [] : (!shared.settings ? [] : [...shared.settings]);

        // 2. add included named shared
        if (!!block.includeShared && shared.namedSettings) {
            const sharedNames = typeof block.includeShared === 'string' ? [ block.includeShared ] : block.includeShared;
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
                return {...settings, ...x};
            } else {
                return x;
            }
        });
        sharedSettings.forEach(shared => {
            const blockSettings = block.settings.find(x => x.id == shared.id);
            if (!!blockSettings) {
                
            }
        });

        // 5. order items
        const settings = [...blockSettings, ...sharedSettings.filter(x => !blockSettings.find(b => b.id === x.id))];
        settings.sort((x, y) => x.sort - y.sort);
        block.settings = settings;


        // const blockSettings = block.settings;
        // // finction to add setting to block settings collection
        // const addSetting = (sharedSettings: ControlDescriptor[], excludePredicate: (string) => boolean = () => false) => {
        //     if (!sharedSettings) return;
        //     sharedSettings.forEach(item => {
        //         if (!excludePredicate(item.id)) {
        //             const settingIndex = blockSettings.findIndex(b => b.id === item.id);
        //             if (settingIndex === -1) {
        //                 blockSettings.push(item);
        //             } else {
        //                 // some of properties can be overriden
        //                 const overrides = blockSettings[settingIndex];
        //                 blockSettings[settingIndex] = { ...item, ...overrides };
        //             }
        //         }
        //     });
        // };
        // // block.includeShared contains name or array of names named settings in shared block
        // if (block.includeShared && shared.namedSettings) {
        //     if (typeof block.includeShared === 'string') {
        //         const sharedSettings = shared.namedSettings[block.includeShared];
        //         addSetting(sharedSettings);
        //     } else {
        //         block.includeShared.forEach(name => {
        //             const sharedSettings = shared.namedSettings[name];
        //             addSetting(sharedSettings);
        //         });
        //     }
        // }
        // // block can skip shared settings or some of them
        // const skipShared = block.excludeShared;
        // if (!skipShared || Array.isArray(skipShared)) {
        //     const exclude = Array.isArray(skipShared) ? (v: string) => (<string[]>skipShared).indexOf(v) !== -1 : () => false;
        //     const sharedSettings = shared.settings;
        //     addSetting(sharedSettings, exclude);
        // }
        // block.settings = [...block.settings.sort(b => b.sort)];
    }
}
