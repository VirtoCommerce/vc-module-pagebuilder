import { AppSettings } from '@app/services';
import { BlocksService } from '@editor/services';
import { of } from 'rxjs';

describe('blocks.service', () => {
    describe('merge settings', () => {

        let platform;
        let service;

        beforeEach(() => {
            platform = jasmine.createSpyObj(['downloadBlocksSchema']);
            service = new BlocksService(<any>platform, <AppSettings>{ contentType: 'page' });
        });

        it('pipe empty settings', (done) => {
            const settings = {};
            platform.downloadBlocksSchema = jasmine.createSpy().and.returnValue(of(settings));

            service.load().subscribe(x => {
                expect(x).toEqual({});
                done();
            });
        });

        it('nothing to merge', (done) => {
            const settings = {
                settings: {
                    name: 'settings',
                    settings: [],
                    static: true
                },
                block1: {
                    name: 'block 1'
                }
            };
            platform.downloadBlocksSchema = jasmine.createSpy().and.returnValue(of(settings));

            service.load().subscribe(x => {
                expect(x).toEqual(settings);
                done();
            });
        });

        it('merge only shared settings', (done) => {
            const settings = {
                settings: { name: 'settings', settings: [], static: true },
                shared: { settings: [{ id: 'id1', name: 'name1' }] },
                block1: { name: 'block 1' }, block2: { name: 'block 2' }
            };
            const result = {
                settings: { name: 'settings', type: 'settings', settings: [], static: true },
                shared: { type: 'shared', settings: [{ id: 'id1', name: 'name1' }] },
                block1: { name: 'block 1', type: 'block1', settings: [{ id: 'id1', name: 'name1' }] },
                block2: { name: 'block 2', type: 'block2', settings: [{ id: 'id1', name: 'name1' }] }
            };
            platform.downloadBlocksSchema = jasmine.createSpy().and.returnValue(of(settings));

            service.load().subscribe(x => {
                // console.log(x);
                // console.log(result);
                expect(x).toEqual(result);
                done();
            });
        });

        it('merge shared and named settings', (done) => {
            const settings = {
                settings: { name: 'settings', settings: [], static: true },
                shared: { settings: [{ id: 'id1', name: 'name1' }], namedSettings: { named1: [{ id: 'named1id1' }, { id: 'named1id2' }], named2: [{ id: 'named2id1' }, { id: 'named2id2' }] } },
                block1: { includeShared: ['named1'], name: 'block 1' },
                block2: { includeShared: 'named2', name: 'block 2' }
            };
            const result = {
                settings: { type: 'settings', name: 'settings', settings: [], static: true },
                shared: { type: 'shared', settings: [{ id: 'id1', name: 'name1' }], namedSettings: { named1: [{ id: 'named1id1' }, { id: 'named1id2' }], named2: [{ id: 'named2id1' }, { id: 'named2id2' }] } },
                block1: { includeShared: ['named1'], name: 'block 1', type: 'block1', settings: [{ id: 'id1', name: 'name1' }, { id: 'named1id1' }, { id: 'named1id2' }] },
                block2: { includeShared: 'named2', name: 'block 2', type: 'block2', settings: [{ id: 'id1', name: 'name1' }, { id: 'named2id1' }, { id: 'named2id2' }] }
            };
            platform.downloadBlocksSchema = jasmine.createSpy().and.returnValue(of(settings));

            service.load().subscribe(x => {
                // console.log(x);
                // console.log(result);
                expect(x).toEqual(result);
                done();
            });
        });

        it('exclude shared true', (done) => {
            const settings = {
                settings: { name: 'settings', settings: [], static: true },
                shared: { settings: [{ id: 'id1', name: 'name1' }], namedSettings: { named1: [{ id: 'named1id1' }, { id: 'named1id2' }], named2: [{ id: 'named2id1' }, { id: 'named2id2' }] } },
                block1: { excludeShared: true, includeShared: ['named1'], name: 'block 1' },
                block2: { includeShared: 'named2', name: 'block 2' }
            };
            const result = {
                settings: { type: 'settings', name: 'settings', settings: [], static: true },
                shared: { type: 'shared', settings: [{ id: 'id1', name: 'name1' }], namedSettings: { named1: [{ id: 'named1id1' }, { id: 'named1id2' }], named2: [{ id: 'named2id1' }, { id: 'named2id2' }] } },
                block1: { excludeShared: true, includeShared: ['named1'], name: 'block 1', type: 'block1', settings: [{ id: 'named1id1' }, { id: 'named1id2' }] },
                block2: { includeShared: 'named2', name: 'block 2', type: 'block2', settings: [{ id: 'id1', name: 'name1' }, { id: 'named2id1' }, { id: 'named2id2' }] }
            };
            platform.downloadBlocksSchema = jasmine.createSpy().and.returnValue(of(settings));

            service.load().subscribe(x => {
                // console.log(x);
                // console.log(result);
                expect(x).toEqual(result);
                done();
            });
        });

        it('exclude shared ids', (done) => {
            const settings = {
                settings: { name: 'settings', settings: [], static: true },
                shared: { settings: [{ id: 'id1', name: 'name1' }], namedSettings: { named1: [{ id: 'named1id1' }, { id: 'named1id2' }], named2: [{ id: 'named2id1' }, { id: 'named2id2' }] } },
                block1: { excludeShared: ['named1id1', 'named2id1'], includeShared: ['named1', 'named2'], name: 'block 1' },
                block2: { includeShared: 'named2', name: 'block 2' }
            };
            const result = {
                settings: { type: 'settings', name: 'settings', settings: [], static: true },
                shared: { type: 'shared', settings: [{ id: 'id1', name: 'name1' }], namedSettings: { named1: [{ id: 'named1id1' }, { id: 'named1id2' }], named2: [{ id: 'named2id1' }, { id: 'named2id2' }] } },
                block1: { excludeShared: ['named1id1', 'named2id1'], includeShared: ['named1', 'named2'], name: 'block 1', type: 'block1', settings: [{ id: 'id1', name: 'name1' }, { id: 'named1id2' }, { id: 'named2id2' }] },
                block2: { includeShared: 'named2', name: 'block 2', type: 'block2', settings: [{ id: 'id1', name: 'name1' }, { id: 'named2id1' }, { id: 'named2id2' }] }
            };
            platform.downloadBlocksSchema = jasmine.createSpy().and.returnValue(of(settings));

            service.load().subscribe(x => {
                // console.log(x);
                // console.log(result);
                expect(x).toEqual(result);
                done();
            });
        });

        it('exclude shared and named', (done) => {
            const settings = {
                settings: { name: 'settings', settings: [], static: true },
                shared: { settings: [{ id: 'id1', name: 'name1' }], namedSettings: { named1: [{ id: 'named1id1' }, { id: 'named1id2' }], named2: [{ id: 'named2id1' }, { id: 'named2id2' }] } },
                block1: { excludeShared: ['id1', 'named1id1', 'named2id1'], includeShared: ['named1', 'named2'], name: 'block 1' },
                block2: { includeShared: 'named2', name: 'block 2' }
            };
            const result = {
                settings: { type: 'settings', name: 'settings', settings: [], static: true },
                shared: { type: 'shared', settings: [{ id: 'id1', name: 'name1' }], namedSettings: { named1: [{ id: 'named1id1' }, { id: 'named1id2' }], named2: [{ id: 'named2id1' }, { id: 'named2id2' }] } },
                block1: { excludeShared: ['id1', 'named1id1', 'named2id1'], includeShared: ['named1', 'named2'], name: 'block 1', type: 'block1', settings: [{ id: 'named1id2' }, { id: 'named2id2' }] },
                block2: { includeShared: 'named2', name: 'block 2', type: 'block2', settings: [{ id: 'id1', name: 'name1' }, { id: 'named2id1' }, { id: 'named2id2' }] }
            };
            platform.downloadBlocksSchema = jasmine.createSpy().and.returnValue(of(settings));

            service.load().subscribe(x => {
                // console.log(x);
                // console.log(result);
                expect(x).toEqual(result);
                done();
            });
        });

        it('override properties from named shared', (done) => {
            const settings = {
                settings: { name: 'settings', settings: [], static: true },
                shared: { namedSettings: { named1: [{ id: 'named1id1', value: 'A', default: 'B' }, { id: 'named1id2' }] } },
                block1: { includeShared: ['named1'], name: 'block 1', settings: [{ id: 'ownId', value: 'ownValue' }, { id: 'named1id1', default: 'C' }] },
            };
            const result = {
                settings: { type: 'settings', name: 'settings', settings: [], static: true },
                shared: { type: 'shared', settings: [], namedSettings: { named1: [{ id: 'named1id1', value: 'A', default: 'B' }, { id: 'named1id2' }] } },
                block1: { includeShared: ['named1'], name: 'block 1', type: 'block1', settings: [{ id: 'ownId', value: 'ownValue' }, { id: 'named1id1', value: 'A', default: 'C' }, { id: 'named1id2' }] },
            };
            platform.downloadBlocksSchema = jasmine.createSpy().and.returnValue(of(settings));

            service.load().subscribe(x => {
                console.log(x);
                console.log(result);
                expect(x).toEqual(result);
                done();
            });
        });

        it('override properties from shared', (done) => {
            const settings = {
                settings: { name: 'settings', settings: [], static: true },
                shared: { settings: [{ id: 'id1', name: 'name1', value: '5' }] },
                block1: { name: 'block 1', settings: [{ id: 'ownId', value: 'ownValue' }, { id: 'id1', value: '7' }] },
            };
            const result = {
                settings: { type: 'settings', name: 'settings', settings: [], static: true },
                shared: { type: 'shared', settings: [{ id: 'id1', name: 'name1', value: '5' }] },
                block1: { name: 'block 1', type: 'block1', settings: [{ id: 'ownId', value: 'ownValue' }, { id: 'id1', name: 'name1', value: '7' }] },
            };
            platform.downloadBlocksSchema = jasmine.createSpy().and.returnValue(of(settings));

            service.load().subscribe(x => {
                console.log(x);
                console.log(result);
                expect(x).toEqual(result);
                done();
            });
        });
    });
});
