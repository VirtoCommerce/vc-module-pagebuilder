import { readonly, ref } from "vue";

interface IStoreItem {
  url: string;
  languages: string[];
}

export type StoreContextStatus = "idle" | "missing" | "loading" | "ready" | "notFound" | "error";

const cache = new Map<string, IStoreItem | null>();
const pendingRequests = new Map<string, Promise<IStoreItem | null>>();
const storeId = ref<string | null>(null);
const storeContextStatus = ref<StoreContextStatus>("idle");
const storeContextError = ref<string | null>(null);

function setStoreContextStatus(
  expectedStoreId: string | null,
  status: StoreContextStatus,
  error: string | null = null,
) {
  if (storeId.value !== expectedStoreId) {
    return;
  }

  storeContextStatus.value = status;
  storeContextError.value = error;
}

export default () => {
  const initUrlParams = () => {
    const urlParams = new URLSearchParams(window.location.search);
    const nextStoreId = urlParams.get("storeId");

    if (storeId.value !== nextStoreId) {
      storeId.value = nextStoreId;
      storeContextError.value = null;
      storeContextStatus.value = nextStoreId ? "idle" : "missing";
      return;
    }

    if (!nextStoreId && storeContextStatus.value === "idle") {
      storeContextStatus.value = "missing";
    }
  };

  async function getStoreUrl(): Promise<string | null> {
    const store = await getStore();
    if (store) {
      return store.url;
    }
    return null;
  }

  async function getLanguages(): Promise<string[]> {
    const store = await getStore();
    if (store) {
      return store.languages ?? [];
    }
    return [];
  }

  async function validateStoreContext(): Promise<boolean> {
    return (await getStore()) !== null;
  }

  async function getStore(): Promise<IStoreItem | null> {
    if (!storeId.value) {
      initUrlParams();
    }

    if (storeId.value && cache.has(storeId.value)) {
      const cachedStore = cache.get(storeId.value) || null;
      setStoreContextStatus(storeId.value, cachedStore ? "ready" : "notFound");
      return cachedStore;
    }

    if (!storeId.value) {
      setStoreContextStatus(null, "missing");
      return null;
    }

    const currentStoreId = storeId.value;

    if (!pendingRequests.has(currentStoreId)) {
      pendingRequests.set(
        currentStoreId,
        fetchStore(currentStoreId).finally(() => pendingRequests.delete(currentStoreId)),
      );
    }

    setStoreContextStatus(currentStoreId, "loading");
    return (await pendingRequests.get(currentStoreId)) || null;
  }

  async function fetchStore(currentStoreId: string): Promise<IStoreItem | null> {
    try {
      const result = await fetch(`/api/stores/${encodeURIComponent(currentStoreId)}`);

      if (!result.ok) {
        if (result.status === 404) {
          cache.set(currentStoreId, null);
          setStoreContextStatus(currentStoreId, "notFound");
        } else {
          setStoreContextStatus(currentStoreId, "error", `Store request failed with status ${result.status}.`);
        }

        return null;
      }

      const store = await result.json();

      if (!store) {
        cache.set(currentStoreId, null);
        setStoreContextStatus(currentStoreId, "notFound");
        return null;
      }

      if (!Array.isArray(store.languages)) {
        console.warn("Invalid store payload", store);
        setStoreContextStatus(currentStoreId, "error", "Store payload is invalid.");
        return null;
      }

      cache.set(currentStoreId, store);
      setStoreContextStatus(currentStoreId, "ready");
      return store || null;
    } catch (error) {
      setStoreContextStatus(currentStoreId, "error", error instanceof Error ? error.message : String(error));
      return null;
    }
  }

  return {
    storeId,
    storeContextStatus: readonly(storeContextStatus),
    storeContextError: readonly(storeContextError),
    initUrlParams,
    validateStoreContext,
    getStoreUrl,
    getLanguages,
  };
};
