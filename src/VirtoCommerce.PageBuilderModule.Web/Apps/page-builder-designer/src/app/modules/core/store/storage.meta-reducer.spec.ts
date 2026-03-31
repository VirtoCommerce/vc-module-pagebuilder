import { storageMetaReducer } from './storage.meta-reducer';
import { LocalStorageService } from './local-storage.service';

describe('storageMetaReducer', () => {
    let storageService: { getItem: ReturnType<typeof vi.fn>; setItem: ReturnType<typeof vi.fn> };
    const baseReducer = (state: any, _action: any) => state ?? { count: 0, name: 'init' };

    beforeEach(() => {
        storageService = {
            getItem: vi.fn().mockReturnValue(null),
            setItem: vi.fn(),
        };
    });

    it('merges saved state on first call (init)', () => {
        storageService.getItem.mockReturnValue({ count: 5 });
        const metaReducer = storageMetaReducer(['count'], 'app-state', storageService as any);
        const reducer = metaReducer(baseReducer);

        const result = reducer(undefined, { type: '@@INIT' });

        expect(result).toEqual({ count: 5, name: 'init' });
        expect(storageService.getItem).toHaveBeenCalledWith('app-state');
    });

    it('does not merge on subsequent calls', () => {
        storageService.getItem.mockReturnValue({ count: 5 });
        const metaReducer = storageMetaReducer(['count'], 'app-state', storageService as any);
        const reducer = metaReducer(baseReducer);

        // First call (init)
        reducer(undefined, { type: '@@INIT' });

        // Second call — should save, not merge
        const result = reducer({ count: 10, name: 'test' }, { type: 'INCREMENT' });

        expect(result).toEqual({ count: 10, name: 'test' });
        expect(storageService.getItem).toHaveBeenCalledTimes(1);
    });

    it('saves only specified keys to storage', () => {
        const metaReducer = storageMetaReducer(['count'], 'app-state', storageService as any);
        const reducer = metaReducer(baseReducer);

        // Init call
        reducer(undefined, { type: '@@INIT' });

        // Subsequent call triggers save
        reducer({ count: 10, name: 'test' }, { type: 'UPDATE' });

        expect(storageService.setItem).toHaveBeenCalledWith('app-state', { count: 10 });
    });

    it('handles null saved state gracefully', () => {
        storageService.getItem.mockReturnValue(null);
        const metaReducer = storageMetaReducer(['count'], 'app-state', storageService as any);
        const reducer = metaReducer(baseReducer);

        const result = reducer(undefined, { type: '@@INIT' });

        expect(result).toEqual({ count: 0, name: 'init' });
    });
});
