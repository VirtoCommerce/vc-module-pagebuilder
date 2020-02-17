import { ServiceLocator } from './service-locator';
import { EventsDispatcher } from './events.dispatcher';
import { BlockViewModel } from './block.view-model';

export class App {
    private list: BlockViewModel[] = [];

    constructor(private dispatcher: EventsDispatcher) { }

    run() {
        this.reloadResources();
        this.dispatcher.handleMessage =
            (handler, msg) => {
                handler.execute(msg, this.list);
            };
        this.dispatcher.run();
        // ServiceLocator.getMessages().ping();
    }

    public getList(): BlockViewModel[] {
        return this.list;
    }

    private reloadResources() {
        var urlParams = new URLSearchParams(window.location.search);
        var prefix = urlParams.get('preview_mode');
        var suffix = "preview_mode=" + prefix + "&v=" + (new Date().getTime());

        var nodes = document.getElementsByTagName("link");

        function generateLinkNode(url) {
            var result = document.createElement("link");
            result.setAttribute("rel", "stylesheet");
            result.setAttribute("type", "text/css");
            result.setAttribute("href", url);
            return result;
        }

        for (var i = 0; i < nodes.length; i++) {
            var styleSheet = nodes[i];
            if (this.isLocalStylesheet(styleSheet.href)) {
                var url = `${styleSheet.href}${styleSheet.href.indexOf('?') != -1 ? '&' : '?'}${suffix}`;
                var newLink = generateLinkNode(url);
                var parent = styleSheet.parentElement;
                try {
                    parent.appendChild(newLink);
                    styleSheet.remove();
                } catch (error) {
                    console.error('couldn\'t replace styles', error)
                }
            }
        }
    }
    
    private isLocalStylesheet(href: string): boolean {
        var result = href && href.startsWith(document.location.origin) && href.indexOf(".css?") != -1;
        return result;
    }
}
