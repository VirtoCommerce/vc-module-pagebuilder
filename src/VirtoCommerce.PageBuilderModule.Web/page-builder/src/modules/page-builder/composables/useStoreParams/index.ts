import { ref } from "vue";

interface IStoreItem {
  url: string;
  languages: string[];
}

const cache = new Map<string, IStoreItem>();

export default () => {
  const storeId = ref<string | null>(null);

  const initUrlParams = () => {
    const urlParams = new URLSearchParams(window.location.search);
    storeId.value = urlParams.get("storeId");
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
      return store.languages;
    }
    return [];
  }

  async function getStore(): Promise<IStoreItem | null> {
    if (!storeId.value) {
      initUrlParams();
    }
    if (storeId.value && cache.has(storeId.value)) {
      return cache.get(storeId.value) || null;
    }
    if (!storeId.value) {
      return null;
    }
    const result = await fetch(`/api/stores/${storeId.value}`);
    const store = await result.json();
    if (store) {
      cache.set(storeId.value, store);
    }
    return store || null;
  }

  return {
    storeId,
    initUrlParams,
    getStoreUrl,
    getLanguages,
  };
};
