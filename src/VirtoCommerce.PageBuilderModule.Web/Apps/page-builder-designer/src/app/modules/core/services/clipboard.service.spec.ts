import { TestBed } from '@angular/core/testing';
import { Clipboard } from '@angular/cdk/clipboard';
import { ClipboardService } from './clipboard.service';
import { EnvironmentRef } from '@integration/services';

describe('ClipboardService', () => {
    let service: ClipboardService;
    let clipboardSpy: { copy: ReturnType<typeof vi.fn> };
    let navigatorClipboard: { readText: ReturnType<typeof vi.fn> };

    beforeEach(() => {
        clipboardSpy = { copy: vi.fn() };
        navigatorClipboard = { readText: vi.fn() };

        TestBed.configureTestingModule({
            providers: [
                ClipboardService,
                { provide: Clipboard, useValue: clipboardSpy },
                { provide: EnvironmentRef, useValue: { navigator: { clipboard: navigatorClipboard } } },
            ],
        });
        service = TestBed.inject(ClipboardService);
    });

    // ── copy ──────────────────────────────────────────────────────

    describe('copy', () => {
        it('serializes ClipboardModel to JSON and copies', () => {
            service.copy({ content: { type: 'hero' }, type: 'section' });
            expect(clipboardSpy.copy).toHaveBeenCalledWith(
                JSON.stringify({ content: { type: 'hero' }, type: 'section' })
            );
        });
    });

    // ── copyString ────────────────────────────────────────────────

    describe('copyString', () => {
        it('copies string to clipboard', () => {
            service.copyString('hello');
            expect(clipboardSpy.copy).toHaveBeenCalledWith('hello');
        });

        it('does not copy null', () => {
            service.copyString(null);
            expect(clipboardSpy.copy).not.toHaveBeenCalled();
        });

        it('does not copy empty string', () => {
            service.copyString('');
            expect(clipboardSpy.copy).not.toHaveBeenCalled();
        });
    });

    // ── getData ───────────────────────────────────────────────────

    describe('getData', () => {
        it('keeps clipboard availability unavailable without the Permissions API', async () => {
            expect(await service.getData(false)).toBeNull();
            expect(navigatorClipboard.readText).not.toHaveBeenCalled();
        });

        it('keeps clipboard availability unavailable when clipboard-read permission is unsupported', async () => {
            Object.defineProperty(TestBed.inject(EnvironmentRef).navigator, 'permissions', {
                value: { query: vi.fn().mockRejectedValue(new TypeError('Unsupported permission')) },
                configurable: true,
            });
            expect(await service.getData(false)).toBeNull();
            expect(navigatorClipboard.readText).not.toHaveBeenCalled();
        });

        it.each(['prompt', 'denied'])('does not request clipboard access while permission is %s', async (state) => {
            const query = vi.fn().mockResolvedValue({ state });
            Object.defineProperty(TestBed.inject(EnvironmentRef).navigator, 'permissions', {
                value: { query }, configurable: true,
            });
            expect(await service.getData(false)).toBeNull();
            expect(query).toHaveBeenCalledWith({ name: 'clipboard-read' });
            expect(navigatorClipboard.readText).not.toHaveBeenCalled();
        });

        it('reads clipboard availability when permission is already granted', async () => {
            Object.defineProperty(TestBed.inject(EnvironmentRef).navigator, 'permissions', {
                value: { query: vi.fn().mockResolvedValue({ state: 'granted' }) }, configurable: true,
            });
            navigatorClipboard.readText.mockResolvedValue('');
            expect(await service.getData(false)).toBeNull();
            expect(navigatorClipboard.readText).toHaveBeenCalledOnce();
        });

        it('parses valid JSON from clipboard', async () => {
            const data = { content: { type: 'hero' }, type: 'section' };
            navigatorClipboard.readText.mockResolvedValue(JSON.stringify(data));

            const result = await service.getData();

            expect(result!.content).toEqual({ type: 'hero' });
            expect(result!.type).toBe('section');
            expect(result!.sourceContent).toBe(JSON.stringify(data));
        });

        it('returns wrongData for invalid JSON', async () => {
            navigatorClipboard.readText.mockResolvedValue('not valid json');

            const result = await service.getData();

            expect(result!.wrongData).toBe(true);
            expect(result!.sourceContent).toBe('not valid json');
        });

        it.each(['null', '42', '"text"', '[]', '{}', '{"type":"section"}',
            '{"type":"page","content":{"type":"hero"}}',
            '{"type":"section","content":{"type":" "}}'])
            ('rejects JSON that is not a section or block: %s', async (data) => {
                navigatorClipboard.readText.mockResolvedValue(data);
                expect(await service.getData()).toEqual({ wrongData: true, sourceContent: data });
            });

        it('accepts copied blocks and shared references', async () => {
            for (const data of [
                { type: 'block', content: { type: 'text' } },
                { type: 'section', content: { id: 'placement', type: 'componentRef', componentRef: 'component' } },
            ]) {
                navigatorClipboard.readText.mockResolvedValue(JSON.stringify(data));
                expect((await service.getData())?.wrongData).not.toBe(true);
            }
        });

        it('returns null for empty clipboard', async () => {
            navigatorClipboard.readText.mockResolvedValue('');

            const result = await service.getData();

            expect(result).toBeNull();
        });

        it('returns null when clipboard access fails', async () => {
            navigatorClipboard.readText.mockRejectedValue(new Error('denied'));

            const result = await service.getData();

            expect(result).toBeNull();
        });
    });
});
