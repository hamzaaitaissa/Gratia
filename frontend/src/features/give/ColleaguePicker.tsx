import { useEffect, useId, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import type { ReadUser } from '../../api/types';
import { Avatar } from '../../components/Avatar';
import { Icon } from '../../components/Icon';
import styles from './GiveDrawer.module.css';

interface ColleaguePickerProps {
  /** Everyone the caller can recognize (themselves excluded). */
  colleagues: ReadUser[];
  value: string | null;
  onChange: (id: string | null) => void;
  error?: string;
}

const jobOf = (u: ReadUser) => u.jobTitle || 'No job title';

/** A combobox (ARIA 1.2 list autocomplete) that searches by name or job title. */
export function ColleaguePicker({ colleagues, value, onChange, error }: ColleaguePickerProps) {
  const id = useId();
  const inputId = `${id}-to`;
  const listId = `${id}-list`;
  const errorId = `${id}-err`;
  const [query, setQuery] = useState('');
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);
  const focusInput = useRef(false);
  const blurTimer = useRef<number | undefined>(undefined);

  const selected = value ? colleagues.find((u) => u.id === value) : undefined;

  const options = useMemo(() => {
    const q = query.trim().toLowerCase();
    return colleagues
      .filter((u) => !q || u.fullName.toLowerCase().includes(q) || (u.jobTitle || '').toLowerCase().includes(q))
      .sort((a, b) => a.fullName.localeCompare(b.fullName));
  }, [colleagues, query]);

  useEffect(() => {
    if (!selected && focusInput.current) {
      focusInput.current = false;
      inputRef.current?.focus();
    }
  }, [selected]);

  useEffect(() => () => window.clearTimeout(blurTimer.current), []);

  const pick = (u: ReadUser) => {
    onChange(u.id);
    setQuery('');
    setOpen(false);
  };

  const onKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setActive((a) => (open ? Math.min(options.length - 1, a + 1) : 0));
      setOpen(true);
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setActive((a) => Math.max(0, a - 1));
    } else if (e.key === 'Enter') {
      e.preventDefault();
      const u = open ? options[active] : undefined;
      if (u) pick(u);
    } else if (e.key === 'Escape' && open) {
      e.preventDefault(); // closes only the list, not the drawer
      setOpen(false);
    }
  };

  const activeOption = open ? options[active] : undefined;

  return (
    <div className={styles.field}>
      {selected ? (
        <>
          <div id={inputId} className="field-label">
            Colleague
          </div>
          <div className={styles.selected} role="group" aria-labelledby={inputId}>
            <Avatar id={selected.id} name={selected.fullName} size={40} />
            <div className={styles.selectedText}>
              <div className={styles.selectedName}>{selected.fullName}</div>
              <div className={styles.selectedJob}>{jobOf(selected)}</div>
            </div>
            <button
              type="button"
              className={styles.change}
              aria-label="Change colleague"
              onClick={() => {
                focusInput.current = true;
                onChange(null);
              }}
            >
              Change
            </button>
          </div>
        </>
      ) : (
        <>
          <label htmlFor={inputId} className="field-label">
            Colleague
          </label>
          <div className={styles.combo}>
            <Icon name="search" className={styles.searchIcon} />
            <input
              ref={inputRef}
              id={inputId}
              data-autofocus=""
              className={`input ${styles.comboInput}`}
              role="combobox"
              aria-autocomplete="list"
              aria-expanded={open}
              aria-controls={listId}
              aria-activedescendant={activeOption ? `${id}-opt-${activeOption.id}` : undefined}
              aria-invalid={error ? true : undefined}
              aria-describedby={error ? errorId : undefined}
              autoComplete="off"
              placeholder="Search by name or job title"
              value={query}
              onChange={(e) => {
                setQuery(e.target.value);
                setOpen(true);
                setActive(0);
              }}
              onFocus={() => setOpen(true)}
              onBlur={() => {
                blurTimer.current = window.setTimeout(() => setOpen(false), 120);
              }}
              onKeyDown={onKeyDown}
            />
            {open && (
              <ul id={listId} role="listbox" aria-label="Colleagues" className={styles.listbox}>
                {options.map((u, i) => (
                  <li
                    key={u.id}
                    id={`${id}-opt-${u.id}`}
                    role="option"
                    aria-selected={i === active}
                    className={styles.option}
                    data-active={i === active || undefined}
                    onMouseDown={(e) => {
                      e.preventDefault(); // keep focus in the input
                      pick(u);
                    }}
                  >
                    <Avatar id={u.id} name={u.fullName} size={34} />
                    <span className={styles.optionText}>
                      <span className={styles.optionName}>{u.fullName}</span>
                      <span className={styles.optionJob}>{jobOf(u)}</span>
                    </span>
                  </li>
                ))}
                {options.length === 0 && (
                  <li role="option" aria-disabled="true" aria-selected="false" className={styles.noOptions}>
                    {colleagues.length === 0 ? 'Nobody else has joined yet.' : `No colleagues match “${query}”.`}
                  </li>
                )}
              </ul>
            )}
          </div>
        </>
      )}
      {error && (
        <p id={errorId} role="alert" className="field-error">
          <Icon name="error" size={18} />
          {error}
        </p>
      )}
    </div>
  );
}
