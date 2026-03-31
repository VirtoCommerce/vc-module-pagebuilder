import { EvaluatorService } from './evaluator.service';

describe('EvaluatorService', () => {
    let service: EvaluatorService;

    beforeEach(() => {
        service = new EvaluatorService();
    });

    // ── evaluate strings ──────────────────────────────────────────

    describe('evaluate (string)', () => {
        it('interpolates template tokens', () => {
            expect(service.evaluate('Hello {{name}}', { name: 'World' })).toBe('Hello World');
        });

        it('evaluates expressions', () => {
            expect(service.evaluate('{{=1+2}}', {})).toBe('3');
        });

        it('returns empty for null value', () => {
            expect(service.evaluate(null, {})).toBeNull();
        });

        it('returns empty string as-is', () => {
            expect(service.evaluate('', {})).toBe('');
        });
    });

    // ── evaluate objects ──────────────────────────────────────────

    describe('evaluate (object)', () => {
        it('recursively evaluates object values', () => {
            const result = service.evaluate({ greeting: 'Hello {{name}}', count: 5 }, { name: 'World' });
            expect(result).toEqual({ greeting: 'Hello World', count: 5 });
        });

        it('handles nested objects', () => {
            const result = service.evaluate({ inner: { msg: '{{x}}' } }, { x: 'val' });
            expect(result).toEqual({ inner: { msg: 'val' } });
        });
    });

    // ── evaluate arrays ───────────────────────────────────────────

    describe('evaluate (array)', () => {
        it('evaluates array elements', () => {
            const result = service.evaluate(['{{a}}', '{{b}}'], { a: 'x', b: 'y' });
            expect(result).toEqual(['x', 'y']);
        });
    });

    // ── evaluate primitives ───────────────────────────────────────

    describe('evaluate (primitives)', () => {
        it('returns number as-is', () => {
            expect(service.evaluate(42, {})).toBe(42);
        });

        it('returns boolean as-is', () => {
            expect(service.evaluate(false, {})).toBe(false);
        });

        it('returns 0 as-is', () => {
            expect(service.evaluate(0, {})).toBe(0);
        });
    });

    // ── evaluateProperty ──────────────────────────────────────────

    describe('evaluateProperty', () => {
        it('evaluates a specific property from config', () => {
            const config = { url: '/api/{{resource}}', name: 'test' };
            const result = service.evaluateProperty(config, 'url', { resource: 'pages' });
            expect(result).toBe('/api/pages');
        });

        it('returns null for missing property', () => {
            expect(service.evaluateProperty({}, 'missing', {})).toBeNull();
        });
    });
});
