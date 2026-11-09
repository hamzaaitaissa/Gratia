/*
 * Theme layer: the company's primary/secondary colours become CSS custom properties on top of a neutral base.
 * Ported from the design's gratia-core.js (G.theme). Every derived tone is pushed until it meets WCAG contrast:
 * fills reach 3:1 against the surfaces, text on fills and brand-tinted text reach 4.5:1.
 */

export type Mode = 'light' | 'dark';
type Rgb = [number, number, number];
type Hsl = [number, number, number];

export const GRATIA_PRIMARY = '#2E2C29';

export const NEUTRAL = {
  light: {
    bg: '#F6F5F2', surface: '#FFFFFF', surface2: '#F0EEEA', line: '#E3E0DA', line2: '#CFCBC3', ink: '#1D1C1A', ink2: '#57534D', ink3: '#6B675F', skel: '#ECE9E4',
    recv: '#0F766E', recvSoft: '#E1F2EF', danger: '#B42318', dangerSoft: '#FCEBE9', ok: '#1E7A46', okSoft: '#E3F3E8', warn: '#8A5A00', warnSoft: '#FBF0D8', info: '#2F5785', infoSoft: '#E5EDF7',
    scrim: 'rgba(24,22,19,.42)',
    sh1: '0 1px 2px rgba(24,22,19,.05), 0 2px 8px rgba(24,22,19,.04)',
    sh2: '0 2px 6px rgba(24,22,19,.06), 0 12px 32px rgba(24,22,19,.10)',
    sh3: '0 8px 24px rgba(24,22,19,.12), 0 32px 64px rgba(24,22,19,.18)',
  },
  dark: {
    bg: '#131312', surface: '#1C1B1A', surface2: '#262523', line: '#33312E', line2: '#47443F', ink: '#F3F1EC', ink2: '#B8B3AA', ink3: '#9C978E', skel: '#2A2927',
    recv: '#5EC8B8', recvSoft: '#15322E', danger: '#FF8A7A', dangerSoft: '#3A1D19', ok: '#6FD39A', okSoft: '#173022', warn: '#F2C265', warnSoft: '#33280F', info: '#9DBCE8', infoSoft: '#1B2636',
    scrim: 'rgba(0,0,0,.6)',
    sh1: '0 1px 2px rgba(0,0,0,.4)',
    sh2: '0 2px 6px rgba(0,0,0,.35), 0 12px 32px rgba(0,0,0,.35)',
    sh3: '0 8px 24px rgba(0,0,0,.45), 0 32px 64px rgba(0,0,0,.5)',
  },
} as const;

const WHITE: Rgb = [255, 255, 255];
const NEAR_BLACK: Rgb = [20, 19, 18];

const clamp01 = (v: number) => Math.max(0, Math.min(1, v));

function hexToRgb(hex: string): Rgb {
  let h = hex.replace('#', '');
  if (h.length === 3) h = h.split('').map((c) => c + c).join('');
  const n = parseInt(h, 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
}

function rgbToHex(rgb: Rgb): string {
  return '#' + rgb.map((v) => Math.round(Math.max(0, Math.min(255, v))).toString(16).padStart(2, '0')).join('').toUpperCase();
}

function luminance(rgb: Rgb): number {
  const [r, g, b] = rgb.map((v) => {
    const c = v / 255;
    return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
  }) as Rgb;
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

function contrastRatio(a: Rgb, b: Rgb): number {
  const l1 = luminance(a);
  const l2 = luminance(b);
  return (Math.max(l1, l2) + 0.05) / (Math.min(l1, l2) + 0.05);
}

const mix = (a: Rgb, b: Rgb, t: number): Rgb => [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];

function rgbToHsl([r0, g0, b0]: Rgb): Hsl {
  const r = r0 / 255, g = g0 / 255, b = b0 / 255;
  const max = Math.max(r, g, b), min = Math.min(r, g, b);
  let h = 0, s = 0;
  const l = (max + min) / 2;
  if (max !== min) {
    const d = max - min;
    s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
    h = max === r ? (g - b) / d + (g < b ? 6 : 0) : max === g ? (b - r) / d + 2 : (r - g) / d + 4;
    h /= 6;
  }
  return [h, s, l];
}

function hslToRgb([h, s, l]: Hsl): Rgb {
  if (!s) return [l * 255, l * 255, l * 255];
  const f = (p: number, q: number, t: number) => {
    if (t < 0) t += 1;
    if (t > 1) t -= 1;
    if (t < 1 / 6) return p + (q - p) * 6 * t;
    if (t < 1 / 2) return q;
    if (t < 2 / 3) return p + (q - p) * (2 / 3 - t) * 6;
    return p;
  };
  const q = l < 0.5 ? l * (1 + s) : l + s - l * s;
  const p = 2 * l - q;
  return [f(p, q, h + 1 / 3) * 255, f(p, q, h) * 255, f(p, q, h - 1 / 3) * 255];
}

/** Shifts lightness (keeping hue and saturation) until the colour reaches `ratio` against `bg`. */
function fit(rgb: Rgb, bg: Rgb, ratio: number): { c: Rgb; adjusted: boolean } {
  if (contrastRatio(rgb, bg) >= ratio) return { c: rgb, adjusted: false };
  const [h, s, l] = rgbToHsl(rgb);
  const dir = luminance(bg) > 0.4 ? -1 : 1;
  for (let k = 1; k <= 50; k++) {
    const c = hslToRgb([h, s, clamp01(l + dir * k * 0.02)]);
    if (contrastRatio(c, bg) >= ratio) return { c, adjusted: true };
  }
  return { c: dir < 0 ? [0, 0, 0] : [255, 255, 255], adjusted: true };
}

export const isValidHex = (h: string | null | undefined) => /^#?[0-9a-fA-F]{6}$/.test(String(h ?? '').trim());

export function normalizeHex(h: string | null | undefined): string {
  let v = String(h ?? '').trim();
  if (!v) return '';
  if (v[0] !== '#') v = '#' + v;
  return v.toUpperCase();
}

export const contrast = (a: string, b: string) => contrastRatio(hexToRgb(a), hexToRgb(b));

export interface ThemeReport {
  raw: string;
  fill: string;
  on: string;
  ink: string;
  secondary: string;
  secondaryDerived: boolean;
  fillAdjusted: boolean;
  inkAdjusted: boolean;
  /** Contrast of the fill against the surface, e.g. "4.6". */
  ratioFill: string;
  /** Contrast of text on the fill. */
  ratioOn: string;
}

export function buildTheme(primary: string | null | undefined, secondary: string | null | undefined, mode: Mode) {
  const N = NEUTRAL[mode];
  const surf = hexToRgb(N.surface);
  const bg = hexToRgb(N.bg);
  const rawHex = isValidHex(primary) ? normalizeHex(primary) : GRATIA_PRIMARY;
  const raw = hexToRgb(rawHex);

  // A UI fill needs 3:1 against the surface and the page background
  let fill = fit(raw, surf, 3).c;
  if (contrastRatio(fill, bg) < 3) fill = fit(fill, bg, 3).c;

  const on = contrastRatio(fill, WHITE) >= contrastRatio(fill, NEAR_BLACK) ? WHITE : NEAR_BLACK;
  if (contrastRatio(fill, on) < 4.5) {
    // Push the fill away from the on-colour until text on it reads at 4.5:1
    const [h, s, l] = rgbToHsl(fill);
    const dir = on === WHITE ? -1 : 1;
    for (let k = 1; k <= 50 && contrastRatio(fill, on) < 4.5; k++) fill = hslToRgb([h, s, clamp01(l + dir * k * 0.02)]);
  }

  const soft = mix(fill, surf, mode === 'dark' ? 0.8 : 0.9);
  const inkFit = fit(raw, soft, 4.5);
  const ink = inkFit.c;
  const [fh, fs, fl] = rgbToHsl(fill);
  const hover = hslToRgb([fh, fs, clamp01(fl + (luminance(fill) > 0.35 ? -0.06 : 0.06))]);

  const secondaryGiven = isValidHex(secondary);
  let secRaw: Rgb;
  if (secondaryGiven) {
    secRaw = hexToRgb(normalizeHex(secondary));
  } else {
    const [h, s, l] = rgbToHsl(raw);
    secRaw = hslToRgb([(h + 0.11) % 1, Math.min(1, s * 0.85 + 0.1), Math.min(0.62, Math.max(0.38, l))]);
  }
  const secSoft = mix(secRaw, surf, mode === 'dark' ? 0.8 : 0.88);
  const secInk = fit(secRaw, secSoft, 4.5).c;
  const onBrand = contrastRatio(raw, WHITE) >= contrastRatio(raw, NEAR_BLACK) ? WHITE : NEAR_BLACK;

  const vars: Record<string, string> = {
    '--g-bg': N.bg, '--g-surface': N.surface, '--g-surface2': N.surface2, '--g-line': N.line, '--g-line2': N.line2,
    '--g-ink': N.ink, '--g-ink2': N.ink2, '--g-ink3': N.ink3, '--g-skel': N.skel,
    '--g-primary': rgbToHex(fill), '--g-primary-hover': rgbToHex(hover), '--g-on-primary': rgbToHex(on), '--g-primary-ink': rgbToHex(ink),
    '--g-primary-soft': rgbToHex(soft), '--g-primary-line': rgbToHex(mix(fill, surf, 0.55)),
    '--g-brand': rawHex, '--g-on-brand': rgbToHex(onBrand),
    '--g-secondary': rgbToHex(secRaw), '--g-secondary-ink': rgbToHex(secInk), '--g-secondary-soft': rgbToHex(secSoft),
    '--g-recv': N.recv, '--g-recv-soft': N.recvSoft, '--g-danger': N.danger, '--g-danger-soft': N.dangerSoft,
    '--g-ok': N.ok, '--g-ok-soft': N.okSoft, '--g-warn': N.warn, '--g-warn-soft': N.warnSoft, '--g-info': N.info, '--g-info-soft': N.infoSoft,
    '--g-focus': rgbToHex(ink), '--g-scrim': N.scrim, '--g-sh1': N.sh1, '--g-sh2': N.sh2, '--g-sh3': N.sh3,
  };

  const report: ThemeReport = {
    raw: rawHex,
    fill: rgbToHex(fill),
    on: rgbToHex(on),
    ink: rgbToHex(ink),
    secondary: rgbToHex(secRaw),
    secondaryDerived: !secondaryGiven,
    fillAdjusted: rgbToHex(fill) !== rawHex,
    inkAdjusted: inkFit.adjusted,
    ratioFill: contrastRatio(fill, surf).toFixed(1),
    ratioOn: contrastRatio(fill, on).toFixed(1),
  };

  return { vars, report };
}

export function applyTheme(el: HTMLElement, vars: Record<string, string>, mode: Mode) {
  for (const [k, v] of Object.entries(vars)) el.style.setProperty(k, v);
  el.style.colorScheme = mode;
}
