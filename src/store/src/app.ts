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
        var suffix = "preview_mode=" + prefix + "&t=" + (new Date().getTime());

        var nodes = document.getElementsByTagName("link");

        function generateLinkNode(url) {
            var result = document.createElement("link");
            result.setAttribute("rel", "stylesheet");
            result.setAttribute("type", "text/css");
            result.setAttribute("href", url);
            return result;
        }

        const itemsToAdd = [];

        for (var i = 0; i < nodes.length; i++) {
            var styleSheet = nodes[i];
            if (this.isLocalStylesheet(styleSheet.href)) {
                var url = `${styleSheet.href}${styleSheet.href.indexOf('?') != -1 ? '&' : '?'}${suffix}`;
                var newLink = generateLinkNode(url);
                itemsToAdd.push({ styleSheet, newLink });
            }
        }

        for (let i = 0; i < itemsToAdd.length; i++) {
            const node = itemsToAdd[i];
            var parent = node.styleSheet.parentElement;
            try {
                parent.appendChild(node.newLink);
                node.styleSheet.remove();
            } catch (error) {
                console.error('couldn\'t replace styles', error)
            }

        }
    }

    private isLocalStylesheet(href: string): boolean {
        var result = href && href.startsWith(document.location.origin) && href.indexOf(".css?") != -1 && href.indexOf('preview_mode') == -1;
        return result;
    }
}
