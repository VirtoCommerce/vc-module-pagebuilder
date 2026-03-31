import { Params } from '@angular/router';
import { RouterReducerState } from '@ngrx/router-store';

export const RouterFeatureName = 'router';

export interface RouterStateUrl {
    url: string;
    params: Params;
    queryParams: Params;
    data?: { [key: string]: any };
    isEmpty: boolean;
}

export interface BuilderState {
    router: RouterReducerState<RouterStateUrl>;
}

export const initialState: RouterReducerState<RouterStateUrl> = {
    state: {
        url: '',
        params: {},
        queryParams: {},
        data: {},
        isEmpty: true
    },
    navigationId: 0
}
