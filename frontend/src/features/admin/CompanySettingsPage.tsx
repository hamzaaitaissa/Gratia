import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { api } from '../../api/endpoints';
import { ApiError } from '../../api/http';
import { fieldErrors, queryKeys, useCompany } from '../../api/queries';
import type { ReadCompany } from '../../api/types';
import { Icon } from '../../components/Icon';
import { PageError, PageHeader } from '../../components/Page';
import { CardsSkeleton } from '../../components/States';
import { useToast } from '../../components/Toast';
import { parseUtc } from '../../lib/format';
import { buildTheme, isValidHex, normalizeHex } from '../../theme/theme';
import { rememberBrand, useThemeMode } from '../../theme/ThemeMode';
import { BrandPreview } from './BrandPreview';
import s from './CompanySettings.module.css';

type Field = 'name' | 'logoUrl' | 'primary' | 'secondary' | 'allowance' | 'resetDay';
type Draft = Record<Field, string>;

const SWATCHES = [
  ['Navy', '#1B2A4A'],
  ['Yellow', '#FFD60A'],
  ['Red', '#E0262F'],
] as const;

const draftOf = (c: ReadCompany): Draft => ({
  name: c.name,
  logoUrl: c.logoUrl ?? '',
  primary: c.primaryColor,
  secondary: c.secondaryColor ?? '',
  allowance: String(c.monthlyPointsAllowance),
  resetDay: String(c.pointsResetDay),
});

const MAX_ALLOWANCE = 1000;
const RESET_DAYS = Array.from({ length: 28 }, (_, i) => i + 1);
const isWhole = (v: string, min: number, max: number) => /^\d+$/.test(v.trim()) && Number(v) >= min && Number(v) <= max;

/** 1 → "1st", 22 → "22nd" */
export function ordinal(n: number) {
  const tens = n % 100;
  const suffix = tens >= 11 && tens <= 13 ? 'th' : ({ 1: 'st', 2: 'nd', 3: 'rd' } as Record<number, string>)[n % 10] ?? 'th';
  return n + suffix;
}

/** Admin · Company settings (design 6.10c): name, logo and colours, with a live preview in the draft colours. */
export function CompanySettingsPage() {
  const company = useCompany();
  const header = <PageHeader title="Company settings" sub={company.data ? `Your name, logo, colours and points, as everyone at ${company.data.name} sees them.` : undefined} />;
  if (company.isError) return <>{header}<PageError error={company.error} onRetry={() => void company.refetch()} /></>;
  if (!company.data) return <CardsSkeleton cards={2} />;
  const c = company.data;
  return (
    <>
      {header}
      {/* A new key after saving starts the form again from what the server stored */}
      <SettingsForm key={[c.id, c.name, c.logoUrl, c.primaryColor, c.secondaryColor, c.monthlyPointsAllowance, c.pointsResetDay].join('|')} company={c} />
    </>
  );
}

function SettingsForm({ company }: { company: ReadCompany }) {
  const queryClient = useQueryClient();
  const toast = useToast();
  const { mode } = useThemeMode();
  const [draft, setDraft] = useState<Draft>(() => draftOf(company));
  const [errors, setErrors] = useState<Partial<Record<Field, string>>>({});

  const primaryOk = isValidHex(draft.primary);
  const secondaryOk = !draft.secondary.trim() || isValidHex(draft.secondary);
  const previewPrimary = primaryOk ? normalizeHex(draft.primary) : company.primaryColor;
  const previewSecondary = draft.secondary.trim() && secondaryOk ? normalizeHex(draft.secondary) : '';
  const report = buildTheme(previewPrimary, previewSecondary, 'light').report;
  const saved = draftOf(company);
  const dirty =
    draft.name.trim() !== saved.name ||
    draft.logoUrl.trim() !== saved.logoUrl ||
    normalizeHex(draft.primary) !== normalizeHex(saved.primary) ||
    normalizeHex(draft.secondary) !== normalizeHex(saved.secondary) ||
    draft.allowance.trim() !== saved.allowance ||
    draft.resetDay !== saved.resetDay;

  const set = (field: Field, value: string) => {
    setDraft((d) => ({ ...d, [field]: value }));
    setErrors((e) => ({ ...e, [field]: undefined }));
  };

  const save = useMutation({
    mutationFn: () =>
      api.updateCompany({
        id: company.id,
        name: draft.name.trim(),
        logoUrl: draft.logoUrl.trim() || null,
        primaryColor: normalizeHex(draft.primary),
        // The API needs a secondary colour; left empty, it's the one derived from the primary, as in the preview
        secondaryColor: draft.secondary.trim() ? normalizeHex(draft.secondary) : report.secondary,
        monthlyPointsAllowance: Number(draft.allowance),
        pointsResetDay: Number(draft.resetDay),
      }),
    onSuccess: (updated) => {
      queryClient.setQueryData(queryKeys.company, updated);
      rememberBrand({ primary: updated.primaryColor, secondary: updated.secondaryColor });
      toast('Company settings saved. Everyone sees the new look the next time they open Gratia.');
    },
    onError: (err) => {
      const f = fieldErrors(err);
      const mapped: Partial<Record<Field, string>> = {
        name: f.name,
        logoUrl: f.logoUrl && 'Use a full link starting with https://',
        primary: f.primaryColor && 'Use a hex colour like #1B2A4A.',
        secondary: f.secondaryColor && 'Use a hex colour like #C8A15A, or leave it empty.',
        allowance: f.monthlyPointsAllowance && `Choose between 1 and ${MAX_ALLOWANCE} points.`,
        resetDay: f.pointsResetDay && 'Choose a day from the 1st to the 28th.',
      };
      if (Object.values(mapped).some(Boolean)) setErrors(mapped);
      else toast((err instanceof ApiError && err.problem.detail) || 'We couldn’t save your settings. Try again in a moment.', 'error');
    },
  });

  const submit = () => {
    const errs: typeof errors = {};
    if (!draft.name.trim()) errs.name = 'Enter your company name.';
    if (draft.logoUrl.trim() && !/^https?:\/\/\S+$/i.test(draft.logoUrl.trim())) errs.logoUrl = 'Use a full link starting with https://';
    if (!primaryOk) errs.primary = 'Use a hex colour like #1B2A4A.';
    if (!secondaryOk) errs.secondary = 'Use a hex colour like #C8A15A, or leave it empty.';
    if (!isWhole(draft.allowance, 1, MAX_ALLOWANCE)) errs.allowance = `Choose a whole number from 1 to ${MAX_ALLOWANCE}.`;
    setErrors(errs);
    if (!Object.keys(errs).length) save.mutate();
  };

  const error = (field: Field) =>
    errors[field] && (
      <p id={`settings-${field}-e`} role="alert" className="field-error">
        <Icon name="error" size={18} />
        {errors[field]}
      </p>
    );

  return (
    <div className={s.layout}>
      <div className={s.form}>
        <div className={s.field}>
          <div className={s.labelRow}>
            <label htmlFor="settings-name" className="field-label">
              Company name
            </label>
            <span aria-hidden="true" className={s.count}>
              {draft.name.length} / 100
            </span>
          </div>
          <input
            id="settings-name"
            className="input"
            maxLength={100}
            value={draft.name}
            aria-invalid={errors.name ? true : undefined}
            aria-describedby={errors.name ? 'settings-name-e' : undefined}
            onChange={(e) => set('name', e.target.value)}
          />
          {error('name')}
        </div>

        <div className={s.field}>
          <div className={s.labelRow}>
            <label htmlFor="settings-logo" className="field-label">
              Logo URL <span className={s.optional}>(optional)</span>
            </label>
            <span aria-hidden="true" className={s.count}>
              {draft.logoUrl.length} / 200
            </span>
          </div>
          <input
            id="settings-logo"
            className="input"
            type="url"
            maxLength={200}
            placeholder="https://"
            value={draft.logoUrl}
            aria-invalid={errors.logoUrl ? true : undefined}
            aria-describedby={errors.logoUrl ? 'settings-logoUrl-e' : 'settings-logo-h'}
            onChange={(e) => set('logoUrl', e.target.value)}
          />
          {error('logoUrl')}
          <p id="settings-logo-h" className={s.hint}>
            A public link to a square logo. If it’s missing or can’t load, we show your initials.
          </p>
        </div>

        <div className={s.field}>
          <label htmlFor="settings-primary" className="field-label">
            Primary colour
          </label>
          <div className={s.colorRow}>
            <input
              type="color"
              aria-label="Pick primary colour"
              className={s.picker}
              value={primaryOk ? normalizeHex(draft.primary).toLowerCase() : '#000000'}
              onChange={(e) => set('primary', e.target.value.toUpperCase())}
            />
            <input
              id="settings-primary"
              className={`input ${s.hex}`}
              spellCheck={false}
              value={draft.primary}
              aria-invalid={errors.primary ? true : undefined}
              aria-describedby={errors.primary ? 'settings-primary-e' : undefined}
              onChange={(e) => set('primary', e.target.value)}
            />
          </div>
          {error('primary')}
          <div className={s.swatches}>
            <span className={s.try}>Try</span>
            {SWATCHES.map(([label, hex]) => (
              <button
                key={hex}
                type="button"
                className={s.swatch}
                aria-pressed={normalizeHex(draft.primary) === hex}
                onClick={() => set('primary', hex)}
              >
                <span aria-hidden="true" className={s.swatchDot} style={{ background: hex }} />
                {label}
              </button>
            ))}
          </div>
        </div>

        <div className={s.field}>
          <label htmlFor="settings-secondary" className="field-label">
            Secondary colour <span className={s.optional}>(optional)</span>
          </label>
          <div className={s.colorRow}>
            <input
              type="color"
              aria-label="Pick secondary colour"
              className={s.picker}
              value={(previewSecondary || report.secondary).toLowerCase()}
              onChange={(e) => set('secondary', e.target.value.toUpperCase())}
            />
            <input
              id="settings-secondary"
              className={`input ${s.hex}`}
              spellCheck={false}
              placeholder={`Derived: ${report.secondary}`}
              value={draft.secondary}
              aria-invalid={errors.secondary ? true : undefined}
              aria-describedby={errors.secondary ? 'settings-secondary-e' : 'settings-secondary-h'}
              onChange={(e) => set('secondary', e.target.value)}
            />
            {draft.secondary && (
              <button type="button" className={s.clear} aria-label="Clear secondary colour" onClick={() => set('secondary', '')}>
                <Icon name="close" />
              </button>
            )}
          </div>
          {error('secondary')}
          <p id="settings-secondary-h" className={s.hint}>
            Used for small accents. Leave it empty and we derive one from your primary colour.
          </p>
        </div>

        {report.fillAdjusted && (
          <div role="status" className={s.adjusted}>
            <Icon name="contrast" />
            <span>
              <strong>We adjusted your colour for readability.</strong> Buttons and highlights use {report.fill} instead of {report.raw}, so text on them
              stays readable ({report.ratioOn}:1) and they stand out from the page ({report.ratioFill}:1). Your logo tile keeps the original colour.
            </span>
          </div>
        )}

        <fieldset className={s.points}>
          <legend className={s.pointsTitle}>Points</legend>
          <div className={s.pointsRow}>
            <div className={s.field}>
              <label htmlFor="settings-allowance" className="field-label">
                Points to give each month
              </label>
              <div className={s.withUnit}>
                <input
                  id="settings-allowance"
                  className="input"
                  type="number"
                  inputMode="numeric"
                  min={1}
                  max={MAX_ALLOWANCE}
                  value={draft.allowance}
                  aria-invalid={errors.allowance ? true : undefined}
                  aria-describedby={errors.allowance ? 'settings-allowance-e' : 'settings-allowance-h'}
                  onChange={(e) => set('allowance', e.target.value)}
                />
                <span className={s.unit}>per person</span>
              </div>
              {error('allowance')}
            </div>
            <div className={s.field}>
              <label htmlFor="settings-resetDay" className="field-label">
                Refill day
              </label>
              <select
                id="settings-resetDay"
                className={`input ${s.select}`}
                value={draft.resetDay}
                aria-invalid={errors.resetDay ? true : undefined}
                aria-describedby={errors.resetDay ? 'settings-resetDay-e' : 'settings-resetDay-h'}
                onChange={(e) => set('resetDay', e.target.value)}
              >
                {RESET_DAYS.map((d) => (
                  <option key={d} value={String(d)}>
                    The {ordinal(d)} of each month
                  </option>
                ))}
              </select>
              {error('resetDay')}
            </div>
          </div>
          <p id="settings-allowance-h" className={s.hint}>
            Everyone’s points to give refill to this number at 00:00 UTC on the refill day. Unused points don’t carry over. A new number applies from
            the next refill, on {cycleDate(company.nextPointsResetAt)}.
          </p>
          <p id="settings-resetDay-h" className={s.hint}>
            Moving the day doesn’t refill anyone: the current month runs on until the new day comes round. Days stop at the 28th so every month has one.
          </p>
        </fieldset>

        <div className={s.actions}>
          <button type="button" className="btn btn-primary" disabled={!dirty || save.isPending} aria-busy={save.isPending} onClick={submit}>
            {save.isPending ? 'Saving…' : 'Save changes'}
          </button>
          <button
            type="button"
            className="btn btn-secondary"
            disabled={!dirty || save.isPending}
            onClick={() => {
              setDraft(draftOf(company));
              setErrors({});
            }}
          >
            Discard
          </button>
        </div>
      </div>

      <div className={s.previewColumn}>
        <div className={s.previewHead}>
          <span className={s.previewLabel}>Live preview</span>
          {dirty && <span className={s.unsaved}>Unsaved changes</span>}
        </div>
        <BrandPreview name={draft.name} logoUrl={draft.logoUrl.trim()} primary={previewPrimary} secondary={previewSecondary} mode={mode} />
      </div>
    </div>
  );
}

/** The refill day as configured ("Saturday, November 21"): refills happen at 00:00 UTC, so the date is the UTC one. */
function cycleDate(iso: string) {
  return parseUtc(iso).toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric', timeZone: 'UTC' });
}
