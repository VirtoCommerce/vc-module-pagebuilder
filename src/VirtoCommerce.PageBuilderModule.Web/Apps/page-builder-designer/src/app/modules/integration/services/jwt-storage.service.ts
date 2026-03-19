import { Injectable } from '@angular/core';

@Injectable({
    providedIn: 'root'
})
export class JwtStorageService {

    // todo: move to builder config
    private static readonly STORAGEKEY = 'ls.authenticationData';

    save(info: any) {
        const data = {
            expiresAt: Date.now() + info.expires_in * 1000,
            refreshToken: info.refresh_token,
            token: info.access_token,
            userName: info.userName
        };
        localStorage.setItem(JwtStorageService.STORAGEKEY, JSON.stringify(data));
        return data;
    }

    getInfo() {
        try {
            const jwt = localStorage.getItem(JwtStorageService.STORAGEKEY);
            if (jwt) {
                const info = JSON.parse(jwt);
                return info;
            }
        } catch (e) {
            console.log('wrong auth info');
        }
        return {};
    }
}
