import type { LinkedComponent } from "../types";
import { createLatestRequestTracker } from "../../../utilities/latestRequest";

interface LinkedComponentDetailsLoaderOptions {
  getComponent: (id: string) => Promise<LinkedComponent>;
  getSelectedComponentId: () => string | undefined;
  selectComponent: (component: LinkedComponent) => void;
  applyComponent: (component: LinkedComponent) => void;
  clearSelectedComponent: (id: string) => void;
  onLoadingChange: (loading: boolean) => void;
}

export interface LinkedComponentDetailsLoader {
  load: (component: LinkedComponent) => Promise<LinkedComponent | undefined>;
  invalidate: () => void;
  dispose: () => void;
}

export function createLinkedComponentDetailsLoader(
  options: LinkedComponentDetailsLoaderOptions,
): LinkedComponentDetailsLoader {
  const requests = createLatestRequestTracker(options.onLoadingChange);

  async function load(component: LinkedComponent): Promise<LinkedComponent | undefined> {
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
