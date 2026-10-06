import { readFile, writeFile } from "node:fs/promises";
import { resolve } from "node:path";
import ts from "typescript";

// Keep the asset-search client reproducible without requiring unrelated API updates.
// Run after the standard NSwag generator; reuse its declarations and shared auth base.
const directory = resolve(process.env.APP_API_CLIENT_DIRECTORY || "src/api_client");
const source = await readFile(resolve(directory, "virtocommerce.pagebuildermodule.ts"), "utf8");
const parsed = ts.createSourceFile("client.ts", source, ts.ScriptTarget.Latest, true);
const names = [
  "PageBuilderAssetSearchClient",
  "PageBuilderAssetSearchCriteria",
  "BlobEntry",
  "BlobEntrySearchResult",
  "throwException",
];
const declarations = names.map((name) => {
  const declaration = parsed.statements.find((statement) => statement.name?.getText(parsed) === name);
  if (!declaration) {
    throw new Error(
      `NSwag output is missing ${name}. Generate against a Platform with the current PageBuilder module.`,
    );
  }
  return declaration.getFullText(parsed).trim();
});
const header = source.slice(0, source.indexOf("// ISO 8601"));
const imports = 'import { AuthApiBase, ApiException, type SortInfo } from "./virtocommerce.pagebuildermodule";';
const output = `${header}${imports}\n\n${declarations.join("\n\n")}\n`.replace(/[\t ]+$/gm, "");
await writeFile(resolve(directory, "pagebuilderAssetSearch.ts"), output);
