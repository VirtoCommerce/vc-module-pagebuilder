import defaultConfig from "@vc-shell/framework/tailwind.config";

// One config for both builds (the Admin Shell UI and the standalone app); vc-shell's own classes come with its CSS.
export default {
  prefix: "tw-",
  content: ["./src/**/*.{vue,ts}", "./standalone/**/*.{vue,ts,html}"],
  theme: defaultConfig.theme,
};
