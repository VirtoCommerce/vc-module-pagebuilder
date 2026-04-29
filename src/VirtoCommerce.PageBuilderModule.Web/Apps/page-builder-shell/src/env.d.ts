/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly PACKAGE_VERSION: string;
  readonly APP_AI_AGENT_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
