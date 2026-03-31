import { TestBed } from '@angular/core/testing';
import { Store } from '@ngrx/store';
import { EventsBusService } from './events-bus.service';
import { EventBusArgs } from '../models';

describe('EventsBusService', () => {
    let service: EventsBusService;

    beforeEach(() => {
        TestBed.configureTestingModule({
            providers: [
                EventsBusService,
                { provide: Store, useValue: {} },
            ],
        });
        service = TestBed.inject(EventsBusService);
    });

    it('should be created', () => {
        expect(service).toBeTruthy();
    });

    it('calls handler when event matches', () => {
        const handler = vi.fn();
        service.on(e => e.target === 'preview', handler);

        service.emit({ target: 'preview', payload: 'test' });

        expect(handler).toHaveBeenCalledWith({ target: 'preview', payload: 'test' });
    });

    it('does not call handler for non-matching events', () => {
        const handler = vi.fn();
        service.on(e => e.target === 'preview', handler);

        service.emit({ target: 'platform' });

        expect(handler).not.toHaveBeenCalled();
    });

    it('supports multiple handlers', () => {
        const handler1 = vi.fn();
        const handler2 = vi.fn();
        service.on(e => e.target === 'preview', handler1);
        service.on(e => e.target === 'preview', handler2);

        service.emit({ target: 'preview' });

        expect(handler1).toHaveBeenCalled();
        expect(handler2).toHaveBeenCalled();
    });

    it('stops receiving after unsubscribe', () => {
        const handler = vi.fn();
        const sub = service.on(() => true, handler);

        service.emit({ target: 'preview' });
        expect(handler).toHaveBeenCalledTimes(1);

        sub.unsubscribe();
        service.emit({ target: 'preview' });
        expect(handler).toHaveBeenCalledTimes(1);
    });

    it('handles multiple emits', () => {
        const handler = vi.fn();
        service.on(() => true, handler);

        service.emit({ target: 'preview' });
        service.emit({ target: 'platform' });

        expect(handler).toHaveBeenCalledTimes(2);
    });
});
