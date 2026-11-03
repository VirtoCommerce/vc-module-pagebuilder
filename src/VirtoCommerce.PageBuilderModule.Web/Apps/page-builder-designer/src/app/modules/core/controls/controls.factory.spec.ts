import { ControlsFactory } from './controls.factory';
import { UnknownEditorComponent } from './unknown-editor/unknown-editor.component';

describe('ControlsFactory', () => {
    let factory: ControlsFactory;

    beforeEach(() => {
        factory = new ControlsFactory();
    });

    // ── register / resolve ────────────────────────────────────────

    describe('register + resolve', () => {
        it('resolves registered component', () => {
            const comp = class MockComponent {} as any;
            factory.register('text', comp);
            expect(factory.resolve('text')).toBe(comp);
        });

        it('returns UnknownEditorComponent for unregistered type', () => {
            expect(factory.resolve('nonexistent')).toBe(UnknownEditorComponent);
        });
    });

    // ── registerLazy / resolveAsync ───────────────────────────────

    describe('registerLazy + resolveAsync', () => {
        it('resolves lazy component', async () => {
            const comp = class LazyComponent {} as any;
            factory.registerLazy('calendar', () => Promise.resolve(comp));

            const result = await factory.resolveAsync('calendar');
            expect(result).toBe(comp);
        });

        it('caches lazy component after first load', async () => {
            let callCount = 0;
            const comp = class LazyComponent {} as any;
            factory.registerLazy('calendar', () => {
                callCount++;
                return Promise.resolve(comp);
            });

            await factory.resolveAsync('calendar');
            await factory.resolveAsync('calendar');
            expect(callCount).toBe(1);
        });

        it('returns UnknownEditorComponent for unknown async type', async () => {
            const result = await factory.resolveAsync('nonexistent');
            expect(result).toBe(UnknownEditorComponent);
        });

        it('prefers sync over lazy if both registered', async () => {
            const syncComp = class SyncComponent {} as any;
            const lazyComp = class LazyComponent {} as any;
            factory.register('text', syncComp);
            factory.registerLazy('text', () => Promise.resolve(lazyComp));

            const result = await factory.resolveAsync('text');
            expect(result).toBe(syncComp);
        });
    });

    // ── isLazy ────────────────────────────────────────────────────

    describe('isLazy', () => {
        it('returns true for lazy-only type', () => {
            factory.registerLazy('calendar', () => Promise.resolve(class {} as any));
            expect(factory.isLazy('calendar')).toBe(true);
        });

        it('returns false for sync-registered type', () => {
            factory.register('text', class {} as any);
            expect(factory.isLazy('text')).toBe(false);
        });

        it('returns false for unknown type', () => {
            expect(factory.isLazy('nonexistent')).toBe(false);
        });

        it('returns false after lazy component has been resolved', async () => {
            factory.registerLazy('calendar', () => Promise.resolve(class {} as any));
            await factory.resolveAsync('calendar');
            expect(factory.isLazy('calendar')).toBe(false);
        });
    });
});
