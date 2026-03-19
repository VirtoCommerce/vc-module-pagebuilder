import { createAction, props } from '@ngrx/store';
import { NavigationExtras } from '@angular/router';

export const go = createAction('[router] router go', props<{
    path?: any[];
    queryParams?: object;
    extras?: NavigationExtras;
    data?: any;
}>());
export const jump = createAction('[router] router jump', props<{
    path: any[];
    queryParams?: object;
    extras?: NavigationExtras;
    data?: any;
}>());
export const skipNavigation = createAction('[Router] skip navigation', props<{ reason: string }>());
export const back = createAction('[Router] back');
export const forward = createAction('[Router] forward');
export const change = createAction('[Router] change', props<{ params: any, path: string }>());
