import * as app from "../../locales";
import * as pageBuilder from "../page-builder/locales";
import * as assetLibrary from "../asset-library/locales";
import * as sharedComponents from "../shared-components/locales";

type Locale = keyof typeof app;

// The Admin Shell UI installs one module: per language, the dictionaries the standalone app installs one by one.
// Not the app's language_name: the shell offers every language that has one, and these cover only the Page Builder.
const locales: Record<string, object> = Object.fromEntries(
  (Object.keys(app) as Locale[]).map((locale) => [
    locale,
    {
      COMMON: app[locale].COMMON,
      PAGE_BUILDER: pageBuilder[locale].PAGE_BUILDER,
      ...assetLibrary[locale],
      ...sharedComponents[locale],
    },
  ]),
);

export default locales;
