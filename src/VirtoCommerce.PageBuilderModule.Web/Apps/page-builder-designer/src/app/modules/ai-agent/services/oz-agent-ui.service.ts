import { effect, inject, Injectable, signal } from '@angular/core';
import { LocalStorageService } from '@core/store/local-storage.service';
import { AppConfig } from '@integration/services';

const STORAGE_KEY = 'pbd.ozAgent.ui';

interface OzAgentUiState {
    isOpen: boolean;
    isPinned: boolean;
}

@Injectable({ providedIn: 'root' })
export class OzAgentUiService {

    private readonly storage = inject(LocalStorageService);
    private readonly config = inject(AppConfig);

    // Single source of truth for "is Oz agent integration available".
    // When null, toggle button, panel, and transport all stay inert.
    readonly agentUrl: string | null = (this.config.getValue('ozAgentUrl') as string | undefined) || null;

    private readonly _isOpen = signal(false);
    private readonly _isPinned = signal(false);

    readonly isOpen = this._isOpen.asReadonly();
    readonly isPinned = this._isPinned.asReadonly();

    constructor() {
        const saved = this.storage.getItem(STORAGE_KEY) as OzAgentUiState | null;
        if (saved) {
            this._isOpen.set(!!saved.isOpen);
            this._isPinned.set(!!saved.isPinned);
        }

        effect(() => {
            const state: OzAgentUiState = {
                isOpen: this._isOpen(),
                isPinned: this._isPinned(),
            };
            this.storage.setItem(STORAGE_KEY, state);
        });
    }

    open() {
        this._isOpen.set(true);
    }

    close() {
        this._isOpen.set(false);
    }

    toggle() {
        this._isOpen.update(v => !v);
    }

    togglePin() {
        this._isPinned.update(v => !v);
    }
}
