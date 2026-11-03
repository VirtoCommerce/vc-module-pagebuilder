import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class LocalStorageService {

    setItem(key: string, value: any) {
        localStorage.setItem(key, JSON.stringify(value));
    }

    getItem(key: string): any {
        try {
            const result = localStorage.getItem(key);
            if (result) {
                return JSON.parse(result);
            }
        } catch (error) {
            console.info(`parse ${key} error:`, error)
        }
        return null;
    }

    removeItem(key: string) {
        localStorage.removeItem(key);
    }
}
