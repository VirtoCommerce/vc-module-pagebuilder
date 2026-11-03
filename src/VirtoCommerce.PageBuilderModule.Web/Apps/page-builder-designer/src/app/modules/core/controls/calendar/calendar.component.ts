import { ChangeDetectionStrategy, Component, signal } from '@angular/core';

import {
    MatCalendar,
    MatDatepicker,
    MatDatepickerActions,
    MatDatepickerApply,
    MatDatepickerCancel,
    MatDatepickerInput,
    MatDatepickerToggle,
    MAT_DATEPICKER_SCROLL_STRATEGY_FACTORY_PROVIDER,
} from 'ngv-datepicker';
import { MatInputModule } from '@angular/material/input';

import { BaseControlDirective } from '@core/controls/base-control.directive';
import { CalendarDescriptor } from '@models/controls';
import * as chrono from 'chrono-node/en';

/**
 * source: https://h2qutc.github.io/angular-material-components
 *
 * this component set current time in time mode when control value is null
 * todo: fix this behaviour
 * in this case event should not be fired
 */

@Component({
    selector: 'app-calendar',
    templateUrl: './calendar.component.html',
    styleUrls: ['./calendar.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    providers: [MAT_DATEPICKER_SCROLL_STRATEGY_FACTORY_PROVIDER],
    imports: [
        MatCalendar,
        MatDatepicker,
        MatDatepickerActions,
        MatDatepickerApply,
        MatDatepickerCancel,
        MatDatepickerInput,
        MatDatepickerToggle,
        MatInputModule,
    ]
})
export class CalendarComponent extends BaseControlDirective<CalendarDescriptor> {

    readonly minDate = signal<Date | null>(null);
    readonly maxDate = signal<Date | null>(null);

    private parseDate(value: Date | string | undefined | null): Date | null {
        if (value && typeof value === 'string') {
            return chrono.parseDate(value) ?? null;
        }
        return (value as Date) ?? null;
    }

    protected override descriptorChanged(): void {
        if (this.descriptor) {
            this.minDate.set(this.parseDate(this.descriptor.minDate));
            this.maxDate.set(this.parseDate(this.descriptor.maxDate));
        }
    }

    raiseValueChanged(event: Date) {
        this.onValueChanged(event);
    }
}
