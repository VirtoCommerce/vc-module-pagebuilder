import { ref } from "vue";

export default () => {
  const storeId = ref<string | null>(null);

  const initUrlParams = () => {
    const urlParams = new URLSearchParams(window.location.search);
    storeId.value = urlParams.get("storeId");
  };

  return {
    storeId,
    initUrlParams,
  };
};
