// Runs vite or vitest: `node scripts/vite.mjs <vite|vitest> [args…]` (the npm scripts call it).
//
// Vite can't work under a path containing '#' (it reads it as the start of a URL fragment), and this repo lives
// under Documents\c#\… (TASKS.md #68). So when the project path has a '#', the tool runs from a junction at a
// '#'-free path that points back here. vite.config.ts (resolve.preserveSymlinks) and Node (--preserve-symlinks)
// both keep that path instead of resolving it back. Elsewhere, e.g. in CI, the tool simply runs in place.
import { spawn } from 'node:child_process';
import { createHash } from 'node:crypto';
import { lstatSync, mkdirSync, readlinkSync, rmdirSync, symlinkSync, unlinkSync } from 'node:fs';
import { homedir } from 'node:os';
import { dirname, join, resolve } from 'node:path';

const BINS = { vite: 'vite/bin/vite.js', vitest: 'vitest/vitest.mjs' };
const [tool, ...args] = process.argv.slice(2);
if (!(tool in BINS)) {
  console.error('Usage: node scripts/vite.mjs <vite|vitest> [args…]');
  process.exit(2);
}

const project = resolve(import.meta.dirname, '..');
let cwd = project;
const env = { ...process.env };

if (project.includes('#')) {
  cwd = linkWithoutHash(project);
  env.NODE_OPTIONS = [env.NODE_OPTIONS, '--preserve-symlinks', '--preserve-symlinks-main'].filter(Boolean).join(' ');
}

const child = spawn(process.execPath, [join(cwd, 'node_modules', BINS[tool]), ...args], { cwd, env, stdio: 'inherit' });
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => child.kill(signal));
child.on('exit', (code, signal) => process.exit(code ?? (signal ? 1 : 0)));

/** A junction (a symlink outside Windows) to `target`, under the user's local app data rather than Temp. */
function linkWithoutHash(target) {
  const base = process.env.LOCALAPPDATA ?? join(homedir(), '.cache');
  const id = createHash('sha1').update(target.toLowerCase()).digest('hex').slice(0, 8);
  const link = join(base, 'Gratia', `frontend-${id}`);
  if (link.includes('#')) {
    console.error(`Can't find a path without '#' for ${target}. Move the project to such a path (TASKS.md #68).`);
    process.exit(1);
  }

  let existing = null;
  try {
    if (lstatSync(link).isSymbolicLink()) existing = readlinkSync(link);
  } catch {
    // no link yet
  }
  const same = existing && resolve(existing.replace(/^\\\\\?\\/, '')).toLowerCase() === target.toLowerCase();
  if (existing && !same) removeLinkOnly(link);
  if (!same) {
    mkdirSync(dirname(link), { recursive: true });
    symlinkSync(target, link, 'junction'); // junctions need no admin rights on Windows
    console.log(`Running from ${link} (a link to this folder), because Vite can't work under a path containing '#'.`);
  }
  return link;
}

/** Removes the link itself, never what it points to (both calls are non-recursive). */
function removeLinkOnly(link) {
  try {
    unlinkSync(link);
  } catch {
    rmdirSync(link);
  }
}
