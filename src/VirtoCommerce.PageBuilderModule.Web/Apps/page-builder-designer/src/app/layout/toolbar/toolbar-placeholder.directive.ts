import { Directive, ViewContainerRef, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRouteSnapshot, NavigationEnd, Router } from "@angular/router";
import { filter } from "rxjs";

@Directive({
    selector: '[toolbar-placeholder]'
})
export class ToolbarPlaceholderDirective {

    private readonly router = inject(Router);
    private readonly viewContainerRef = inject(ViewContainerRef);

    private currentToolbar: any;

    constructor() {
        this.router.events.pipe(
            filter(e => e instanceof NavigationEnd),
            takeUntilDestroyed()
        ).subscribe(() => this.updateToolbar());

        // Handle case where initial navigation already completed before this directive was created
        this.updateToolbar();
    }

    private updateToolbar(): void {
        const toolbar = this.findToolbar(this.router.routerState.snapshot.root);

        if (toolbar === null) {
            this.viewContainerRef.clear();
        } else if (toolbar !== this.currentToolbar) {
            this.viewContainerRef.clear();
            this.viewContainerRef.createComponent(toolbar);
            this.currentToolbar = toolbar;
        }
    }

    private findToolbar(node: ActivatedRouteSnapshot): any {
        if (node.data && !!node.data['toolbar']) {
            return node.data['toolbar'];
        }

        if (node.children) {
            for (let child of node.children) {
                const result = this.findToolbar(child);
                if (!!result) {
                    return result;
                }
            }
        }

        return null;
    }
}
