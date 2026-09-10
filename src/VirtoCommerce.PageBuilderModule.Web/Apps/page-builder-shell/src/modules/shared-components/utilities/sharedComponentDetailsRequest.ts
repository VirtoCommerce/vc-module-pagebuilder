import type { SharedComponent } from "../types";
import { createLatestRequestTracker } from "../../../utilities/latestRequest";

interface SharedComponentDetailsLoaderOptions {
  getComponent: (id: string) => Promise<SharedComponent>;
  getSelectedComponentId: () => string | undefined;
  selectComponent: (component: SharedComponent) => void;
  applyComponent: (component: SharedComponent) => void;
  clearSelectedComponent: (id: string) => void;
  onLoadingChange: (loading: boolean) => void;
}

export interface SharedComponentDetailsLoader {
  load: (component: SharedComponent) => Promise<SharedComponent | undefined>;
  invalidate: () => void;
  dispose: () => void;
}

export function createSharedComponentDetailsLoader(
  options: SharedComponentDetailsLoaderOptions,
): SharedComponentDetailsLoader {
  const requests = createLatestRequestTracker(options.onLoadingChange);

  async function load(component: SharedComponent): Promise<SharedComponent | undefined> {
    if (!component.id) {
      return undefined;
    }

    const componentId = component.id;
    const request = requests.begin();
    options.selectComponent(component);

    try {
      const details = await options.getComponent(componentId);

      if (isCurrent(request, componentId)) {
        options.applyComponent(details);
      }

      return details;
    } catch (error) {
      if (isCurrent(request, componentId)) {
        options.clearSelectedComponent(componentId);
        throw error;
      }

      return undefined;
    } finally {
      request.complete();
    }
  }

  function isCurrent(request: { isCurrent: () => boolean }, componentId: string): boolean {
    return request.isCurrent() && options.getSelectedComponentId() === componentId;
  }

  return {
    load,
    invalidate: requests.invalidate,
    dispose: requests.dispose,
  };
}
