import { ChangeDetectionStrategy, Component, OnDestroy, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TemplateEditorComponent } from '@editor/components/template-editor/template-editor.component';
import { PAGE_ANCHORS_PROVIDER, clearActivePageAnchorsProvider, setActivePageAnchorsProvider } from '@core/services';

@Component({
    selector: 'app-template-editor-host',
    templateUrl: './template-editor-host.component.html',
    styleUrls: ['./template-editor-host.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [RouterOutlet, TemplateEditorComponent]
})
export class TemplateEditorHostComponent implements OnDestroy {

    private readonly pageAnchors = inject(PAGE_ANCHORS_PROVIDER);

    /**
     * Publishes the page anchors to the rich-text controls for as long as the editor is on screen.
     *
     * The registration is bound to this component rather than to the service that fills it, because
     * the route `providers` live in an `EnvironmentInjector` the router keeps alive for the whole
     * application unless `withExperimentalAutoCleanupInjectors` is enabled — a service `ngOnDestroy`
     * would never run. This component is created and destroyed with the route itself, so leaving the
     * editor really does take the anchors down with it.
     */
    constructor() {
        setActivePageAnchorsProvider(this.pageAnchors);
    }

    ngOnDestroy(): void {
        clearActivePageAnchorsProvider(this.pageAnchors);
    }
}
