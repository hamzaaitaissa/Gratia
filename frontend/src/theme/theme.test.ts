import { buildTheme, contrast, NEUTRAL } from './theme';

const BRANDS = {
  navy: ['#1B2A4A', '#C8A15A'],
  yellow: ['#FFD60A', ''],
  red: ['#E0262F', '#2B2D42'],
} as const;

describe('buildTheme', () => {
  for (const [name, [primary, secondary]] of Object.entries(BRANDS))
    for (const mode of ['light', 'dark'] as const)
      it(`keeps ${name} readable in ${mode} mode`, () => {
        const { vars } = buildTheme(primary, secondary, mode);
        // Text on a filled button, and brand-tinted text on its soft background
        expect(contrast(vars['--g-primary']!, vars['--g-on-primary']!)).toBeGreaterThanOrEqual(4.5);
        expect(contrast(vars['--g-primary-ink']!, vars['--g-primary-soft']!)).toBeGreaterThanOrEqual(4.5);
        expect(contrast(vars['--g-secondary-ink']!, vars['--g-secondary-soft']!)).toBeGreaterThanOrEqual(4.5);
        expect(vars['--g-bg']).toBe(NEUTRAL[mode].bg);
      });

  it('keeps a navy that already works and only adjusts a bright yellow', () => {
    expect(buildTheme('#1B2A4A', '', 'light').report.fillAdjusted).toBe(false);
    const yellow = buildTheme('#FFD60A', '', 'light').report;
    expect(yellow.fillAdjusted).toBe(true);
    expect(yellow.raw).toBe('#FFD60A'); // the logo tile keeps the original colour
    expect(yellow.secondaryDerived).toBe(true);
  });

  it('falls back to Gratia’s own colour when the primary isn’t a hex colour', () => {
    expect(buildTheme('not-a-colour', null, 'light').report.raw).toBe('#2E2C29');
  });
});
