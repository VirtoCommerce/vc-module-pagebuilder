import { Injectable } from '@angular/core';

@Injectable({
    providedIn: 'root'
})
export class EnvironmentRef {
    get nativeWindow(): Window & { clipboardData: any } {
        return <any>window;
    }

    get navigator(): Navigator & { clipboard: any } {
        return <any>navigator;
    }
}
