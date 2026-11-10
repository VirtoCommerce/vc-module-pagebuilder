import { readonly, ref } from "vue";

interface IStoreItem {
  url: string;
  languages: string[];
  defaultLanguage?: string;
  settings?: { name?: string; value?: unknown }[];
}

const newPageBuilderSetting = "VirtoCommerce.PageBuilderModule.Store.NewPageBuilder";

export type StoreContextStatus = "idle" | "missing" | "loading" | "ready" | "notFound" | "error";

type StoreResponse =
  | { status: "ready"; store: IStoreItem }
  | { status: "notFound" }
  | { status: "error"; error: string };

const cache = new Map<string, IStoreItem | null>();
const pendingRequests = new Map<string, Promise<IStoreItem | null>>();
const storeId = ref<string | null>(null);
const storeContextStatus = ref<StoreContextStatus>("idle");
const storeContextError = ref<string | null>(null);
// Set by a host blade, as the Admin Shell has no ?storeId=: the URL must not decide the store then.
let storeIdPinned = false;

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

async function requestStore(id: string): Promise<StoreResponse> {
  try {
    const result = await fetch(`/api/stores/${encodeURIComponent(id)}`);

    if (!result.ok) {
      return result.status === 404
        ? { status: "notFound" }
        : { status: "error", error: `Store request failed with status ${result.status}.` };
    }

    const store = await result.json();

    if (!store) {
      return { status: "notFound" };
    }

    if (!Array.isArray(store.languages)) {
      console.warn("Invalid store payload", store);
      return { status: "error", error: "Store payload is invalid." };
    }

    return { status: "ready", store };
  } catch (error) {
    return { status: "error", error: error instanceof Error ? error.message : String(error) };
  }
}

export default () => {
  const setStoreId = (id: string, store?: Partial<IStoreItem> | null) => {
    storeIdPinned = true;
    if (store && Array.isArray(store.languages)) {
      cache.set(id, { url: store.url ?? "", languages: store.languages, defaultLanguage: store.defaultLanguage });
    }
    if (storeId.value !== id) {
      storeId.value = id;
      storeContextError.value = null;
      storeContextStatus.value = "idle";
    }
  };

  const initUrlParams = () => {
    if (storeIdPinned) {
      return;
    }
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

  async function getDefaultLanguage(): Promise<string | undefined> {
    const store = await getStore();
    return store?.defaultLanguage ?? undefined;
  }

  /** Read on every call: the store's settings may change while the shell is open. `undefined`: the store couldn't be read. */
  async function isNewPageBuilder(id: string | null = storeId.value): Promise<boolean | undefined> {
    if (!id) {
      return false;
    }
    const response = await requestStore(id);
    if (response.status === "error") {
      return undefined;
    }
    return (
      response.status === "ready" &&
      response.store.settings?.find((x) => x.name === newPageBuilderSetting)?.value === true
    );
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
    const response = await requestStore(currentStoreId);

    if (response.status === "error") {
      setStoreContextStatus(currentStoreId, "error", response.error);
      return null;
    }

    const store = response.status === "ready" ? response.store : null;
    cache.set(currentStoreId, store);
    setStoreContextStatus(currentStoreId, response.status);
    return store;
  }

  return {
    storeId,
    storeContextStatus: readonly(storeContextStatus),
    storeContextError: readonly(storeContextError),
    initUrlParams,
    setStoreId,
    validateStoreContext,
    getStoreUrl,
    getLanguages,
    getDefaultLanguage,
    isNewPageBuilder,
  };
};
