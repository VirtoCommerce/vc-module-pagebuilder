import type { ColorScheme, PaletteTokens } from "./previewMessages";
import { tokenLabel, type ThemeLook } from "./themeLook";

// The palette generator and the contrast check (docs/contracts/theme-snapshot.md, "Contrast pairs" and "Palette
// generation"): the server builds both palettes from one or two colors and names the text and background pairs a
// palette fails, by WCAG 2.2 AA.

/** A pair below its ratio; `text` and `background` are the tokens whose colors were compared */
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
  /** pairs whose colors the palette leaves to the storefront preset */
  unchecked: number;
}

export interface GeneratedPalettes {
  light: Record<string, string>;
  dark: Record<string, string>;
  failures: PaletteFailure[];
}

/** The wait after the last color edit before the palette is checked */
export const PALETTE_CHECK_DEBOUNCE_MS = 500;

const paletteApi = "/api/pagebuilder/theme/palette";

export const generatePaletteUrl = (storeId: string) => `${paletteApi}?${new URLSearchParams({ storeId })}`;

export const checkPaletteUrl = (storeId: string) => `${paletteApi}/check?${new URLSearchParams({ storeId })}`;

export const generateBody = (tokens: PaletteTokens, primary: string, secondary?: string | null) => ({
  primary,
  ...(secondary ? { secondary } : {}),
  tokens,
});

export const checkBody = (tokens: PaletteTokens, look: ThemeLook) => ({ palette: { light: look.light, dark: look.dark }, tokens });

/** The palettes as they were before a change, for its undo */
export interface PaletteStep {
  light: Record<string, string>;
  dark: Record<string, string>;
}

const MAX_STEPS = 50;

/** The stack with the look's palettes on top, the oldest dropped past the limit */
export const withStep = (steps: PaletteStep[], look: ThemeLook): PaletteStep[] => [...steps, { light: { ...look.light }, dark: { ...look.dark } }].slice(-MAX_STEPS);

/** Both generated palettes in place of the look's, the brand kept: one step to undo */
export function withGenerated(look: ThemeLook, steps: PaletteStep[], generated: GeneratedPalettes): { look: ThemeLook; steps: PaletteStep[] } {
  return { look: { ...look, light: { ...generated.light }, dark: { ...generated.dark } }, steps: withStep(steps, look) };
}

/** The look with the last step's palettes back, the brand as it is now; nothing to undo: undefined */
export function undoStep(look: ThemeLook, steps: PaletteStep[]): { look: ThemeLook; steps: PaletteStep[] } | undefined {
  const last = steps[steps.length - 1];
  return last ? { look: { ...look, light: last.light, dark: last.dark }, steps: steps.slice(0, -1) } : undefined;
}

/** "4.5:1", "2.11:1" */
export const formatRatio = (ratio: number) => `${Number(ratio.toFixed(2))}:1`;

/** "button-solid-primary" → "Button solid primary" */
export function pairLabel(pair: string): string {
  const words = pair.replace(/-/g, " ").trim();
  return words ? words[0].toUpperCase() + words.slice(1) : pair;
}

/** A pair of tokens below AA, as the palette shows it: one row however many of the storefront's pairs use it */
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

/** The scheme's failures, one row per text and background token, the worst first */
export function failureRows(failures: PaletteFailure[], scheme: ColorScheme): FailureRow[] {
  const rows = new Map<string, { failure: PaletteFailure; required: number; pairs: string[] }>();
  for (const failure of failures.filter((x) => x.scheme === scheme)) {
    const key = `${failure.text}|${failure.background}`;
    const row = rows.get(key);
    if (row) {
      row.required = Math.max(row.required, failure.minimumRatio);
      row.pairs.push(pairLabel(failure.pair));
    } else rows.set(key, { failure, required: failure.minimumRatio, pairs: [pairLabel(failure.pair)] });
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

/** The failures of a scheme */
export const failureCount = (failures: PaletteFailure[], scheme: ColorScheme) => failures.filter((x) => x.scheme === scheme).length;

const isRecord = (value: unknown): value is Record<string, unknown> => !!value && typeof value === "object" && !Array.isArray(value);

const readFailures = (value: unknown): PaletteFailure[] => (Array.isArray(value) ? (value.filter(isRecord) as unknown as PaletteFailure[]) : []);

const readColors = (value: unknown): Record<string, string> =>
  isRecord(value) ? (Object.fromEntries(Object.entries(value).filter(([, color]) => typeof color === "string")) as Record<string, string>) : {};

export function parseGenerated(body: unknown): GeneratedPalettes {
  const source = isRecord(body) ? body : {};
  return { light: readColors(source.light), dark: readColors(source.dark), failures: readFailures(source.failures) };
}

export function parseCheck(body: unknown): PaletteCheck {
  const source = isRecord(body) ? body : {};
  return { failures: readFailures(source.failures), unchecked: typeof source.unchecked === "number" ? source.unchecked : 0 };
}
