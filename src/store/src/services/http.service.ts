export class HttpService {

    private token: string;

    constructor(private endpoint: string) { }

    get() {

    }

    postTo(endpoint: string, model: any) {
        // https://gist.github.com/codecorsair/e14ec90cee91fa8f56054afaa0a39f13
        return new Promise<string>((resolve, reject) => {
            const xhr = new XMLHttpRequest();
            xhr.open('post', endpoint);

            xhr.setRequestHeader('Accept', 'application/json, text/javascript, text/plain')
            xhr.setRequestHeader('Cache-Control', 'no-cache');
            xhr.setRequestHeader('Content-Type', 'application/json');
            xhr.setRequestHeader('X-XSRF-TOKEN', this.getToken());
            xhr.send(JSON.stringify(model));
            // xhr.timeout = timeout;
            xhr.onload = evt => {
                // const result = {
                //     ok: xhr.status >= 200 && xhr.status < 300,
                //     status: xhr.status,
                //     statusText: xhr.statusText,
                //     headers: xhr.getAllResponseHeaders(),
                //     data: xhr.responseText
                // };
                resolve(xhr.responseText.trim());
                // this.blocks.push(model); or replace
            };
            // xhr.onerror = evt => {
            //   resolve(errorResponse(xhr, 'Failed to make request.'));
            // };
            // xhr.ontimeout = evt => {
            //   resolve(errorResponse(xhr, 'Request took longer than expected.'));
            // };
        });
    }

    post(model: any): Promise<string> {
        return this.postTo(this.endpoint, model);
    }

    private getToken(): string {
        if (!this.token){
            var all = document.cookie;
            var parts = all.split(';');
            var xsrf = null;
            for (var i = 0; i < parts.length; i++) {
                var part = parts[i];
                if (part.startsWith(' XSRF-TOKEN') || part.startsWith('XSRF-TOKEN')){
                    xsrf = part.substr(part.indexOf('XSRF-TOKEN=') + 11)
                    break;
                }
            }
            this.token = xsrf;
        }
        return this.token;
    }

/*




*/




}