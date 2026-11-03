import { BaseControlDescriptor } from "./base-control.descriptor";

export interface CalendarDescriptor extends BaseControlDescriptor {
    mode: 'date' | 'datetime' | 'time' | 'month' | 'year';
    minDate?: Date | string | null;
    maxDate?: Date | string | null;
    inline?: boolean;
}
