import indexHtml from '../../index.html?raw';
import { ICON_NAMES } from './Icon';

// The icon font is a subset: an icon missing from index.html would render as its name in plain text
it('index.html loads exactly the icons in ICON_NAMES, alphabetically', () => {
  const match = /icon_names=([a-z_,]+)/.exec(indexHtml);
  expect(match).not.toBeNull();
  const loaded = match![1]!.split(',');
  expect(loaded).toEqual([...ICON_NAMES].sort());
  expect(loaded).toEqual([...ICON_NAMES]);
});
