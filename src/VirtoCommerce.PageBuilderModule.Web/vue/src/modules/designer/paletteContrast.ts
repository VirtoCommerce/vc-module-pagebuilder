import type { ThemePaletteCheckRequest, ThemePaletteGenerationRequest } from "../../api_client/virtocommerce.pagebuildermodule";
import { isRecord } from "./json";
import type { ColorScheme, PaletteTokens } from "./previewMessages";
import { humanize } from "./schema";
import { readColors, tokenLabel, type ThemeLook } from "./themeLook";

export interface PaletteFailure {
  scheme: ColorScheme;
  pair: string;
  text: string;
  background: string;
  textColor: string;
  backgroundColor: string;
  ratio: number;
  minimumRatio: number;
}

export interface PaletteCheck {
  failures: PaletteFailure[];
  unchecked: number;
}

export type PaletteStep = Pick<ThemeLook, "light" | "dark">;

export interface GeneratedPalettes extends PaletteStep {
  check: PaletteCheck;
}

export const PALETTE_CHECK_DEBOUNCE_MS = 500;

const paletteApi = "/api/pagebuilder/theme/palette";

export const generatePaletteUrl = (storeId: string) => `${paletteApi}?${new URLSearchParams({ storeId })}`;

export const checkPaletteUrl = (storeId: string) => `${paletteApi}/check?${new URLSearchParams({ storeId })}`;

export const generateBody = (tokens: PaletteTokens, primary: string, secondary?: string | null): ThemePaletteGenerationRequest => ({
  primary,
  ...(secondary ? { secondary } : {}),
  tokens,
});

export const checkBody = (tokens: PaletteTokens, look: ThemeLook): ThemePaletteCheckRequest => ({ palette: { light: look.light, dark: look.dark }, tokens });

const MAX_STEPS = 50;

export const withStep = (steps: PaletteStep[], look: ThemeLook): PaletteStep[] => [...steps, { light: { ...look.light }, dark: { ...look.dark } }].slice(-MAX_STEPS);

export function withGenerated(look: ThemeLook, steps: PaletteStep[], generated: GeneratedPalettes): { look: ThemeLook; steps: PaletteStep[] } {
  return { look: { ...look, light: { ...generated.light }, dark: { ...generated.dark } }, steps: withStep(steps, look) };
}

export function undoStep(look: ThemeLook, steps: PaletteStep[]): { look: ThemeLook; steps: PaletteStep[] } | undefined {
  const last = steps[steps.length - 1];
  return last ? { look: { ...look, light: last.light, dark: last.dark }, steps: steps.slice(0, -1) } : undefined;
}

export const formatRatio = (ratio: number) => `${Number(ratio.toFixed(2))}:1`;

export interface FailureRow {
  key: string;
  text: string;
  background: string;
  textColor: string;
  backgroundColor: string;
  ratio: string;
  required: string;
  pairs: string[];
}

export function failureRows(failures: PaletteFailure[], scheme: ColorScheme): FailureRow[] {
  const rows = new Map<string, { failure: PaletteFailure; required: number; pairs: string[] }>();
  for (const failure of failures.filter((x) => x.scheme === scheme)) {
    const key = `${failure.text}|${failure.background}`;
    const row = rows.get(key);
    if (row) {
      row.required = Math.max(row.required, failure.minimumRatio);
      row.pairs.push(humanize(failure.pair));
    } else rows.set(key, { failure, required: failure.minimumRatio, pairs: [humanize(failure.pair)] });
  }
  return [...rows]
    .sort(([, a], [, b]) => a.failure.ratio - b.failure.ratio)
    .map(([key, { failure, required, pairs }]) => ({
      key,
      text: tokenLabel(failure.text),
      background: tokenLabel(failure.background),
      textColor: failure.textColor,
      backgroundColor: failure.backgroundColor,
      ratio: formatRatio(failure.ratio),
      required: formatRatio(required),
      pairs,
    }));
}

export const failureCount = (failures: PaletteFailure[], scheme: ColorScheme) => failures.filter((x) => x.scheme === scheme).length;

const readFailures = (value: unknown): PaletteFailure[] => (Array.isArray(value) ? (value.filter(isRecord) as unknown as PaletteFailure[]) : []);

export function parseCheck(body: unknown): PaletteCheck {
  const source = isRecord(body) ? body : {};
  return { failures: readFailures(source.failures), unchecked: typeof source.unchecked === "number" ? source.unchecked : 0 };
}

export function parseGenerated(body: unknown): GeneratedPalettes {
  const source = isRecord(body) ? body : {};
  return { light: readColors(source.light), dark: readColors(source.dark), check: parseCheck(body) };
}
