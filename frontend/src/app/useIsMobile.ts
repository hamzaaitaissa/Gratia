import { useSyncExternalStore } from 'react';

// The design switches from the sidebar to the bottom tab bar below 900px
const QUERY = '(max-width: 899.98px)';

function subscribe(onChange: () => void) {
  if (typeof matchMedia !== 'function') return () => {};
  const mql = matchMedia(QUERY);
  mql.addEventListener('change', onChange);
  return () => mql.removeEventListener('change', onChange);
}

const getSnapshot = () => (typeof matchMedia === 'function' ? matchMedia(QUERY).matches : false);

export function useIsMobile(): boolean {
  return useSyncExternalStore(subscribe, getSnapshot, () => false);
}
