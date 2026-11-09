import { avatarColors, initials } from '../lib/format';

interface AvatarProps {
  id: string;
  name: string;
  size: number;
  /** Shows "?" instead of initials, e.g. the preview before a colleague is chosen. */
  unknown?: boolean;
  className?: string;
}

/** Decorative: the name is always written next to it. */
export function Avatar({ id, name, size, unknown, className }: AvatarProps) {
  const c = avatarColors(id);
  return (
    <span
      aria-hidden="true"
      className={'avatar' + (className ? ' ' + className : '')}
      style={{ width: size, height: size, background: c.bg, color: c.fg, fontSize: Math.round(size * 0.36) }}
    >
      {unknown ? '?' : initials(name)}
    </span>
  );
}
