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
