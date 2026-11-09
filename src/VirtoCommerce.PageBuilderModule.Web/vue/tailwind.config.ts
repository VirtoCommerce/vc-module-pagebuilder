import defaultConfig from "@vc-shell/framework/tailwind.config";

// vc-shell's own classes come with its CSS, so its sources are not scanned.
export default {
  prefix: "tw-",
  content: ["./src/**/*.{vue,ts}", "./standalone/**/*.{vue,ts,html}"],
  theme: defaultConfig.theme,
};
