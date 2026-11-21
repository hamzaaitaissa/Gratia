import type { CSSProperties } from 'react';

/**
 * Material Symbols Rounded, loaded from Google Fonts as a subset of just these icons.
 * Adding an icon here means adding it to the `icon_names` list in index.html too (alphabetical);
 * Icon.test.ts fails until both lists match.
 */
export const ICON_NAMES = [
  'add', 'arrow_back', 'arrow_downward', 'arrow_forward', 'arrow_upward', 'badge', 'block', 'check', 'check_circle',
  'chevron_right', 'close', 'cloud_alert', 'cloud_off', 'content_copy', 'contrast', 'dark_mode', 'description',
  'devices', 'download', 'edit', 'error', 'explore_off', 'forum', 'forward_to_inbox', 'group', 'group_add', 'history',
  'home', 'hourglass_top', 'how_to_reg', 'key', 'light_mode', 'link_off', 'lock', 'lock_clock', 'logout',
  'mark_email_read', 'palette', 'person', 'person_add', 'person_remove', 'person_search', 'refresh', 'remove',
  'restart_alt', 'schedule', 'schedule_send', 'search', 'send', 'shield_person', 'stars', 'table_view', 'timer_off',
  'unfold_more', 'upload_file', 'visibility_off', 'volunteer_activism', 'warning',
] as const;

export type IconName = (typeof ICON_NAMES)[number];

interface IconProps {
  name: IconName;
  size?: number;
  filled?: boolean;
  className?: string;
  style?: CSSProperties;
}

/** Decorative only: give the surrounding control its own label. */
export function Icon({ name, size = 20, filled = false, className, style }: IconProps) {
  return (
    <span
      aria-hidden="true"
      className={'ms' + (className ? ' ' + className : '')}
      style={{ fontSize: size, fontVariationSettings: filled ? "'FILL' 1" : undefined, ...style }}
    >
      {name}
    </span>
  );
}
