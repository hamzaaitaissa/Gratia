// Regenerates src/api/schema.d.ts from the API's OpenAPI document (TASKS.md #59):
//
//   npm run api:types                       # from the API in Development: http://localhost:5290/openapi/v1.json
//   npm run api:types -- <url or file>
//
// Run it after any change to the API's DTOs; `npm run typecheck` then shows what the frontend has to follow.
// openapi-typescript runs through npx, pinned with the TypeScript 5 it was written for, rather than as a project
// dependency: it declares TypeScript 5 as a peer, and the project builds with TypeScript 7.
// It prints to stdout and this script writes the file: given an output path, openapi-typescript turns it into a URL,
// which cuts it at the '#' of "Documents\c#\…" (TASKS.md #68) and writes somewhere else.
import { spawnSync } from 'node:child_process';
import { writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

const source = process.argv[2] ?? 'http://localhost:5290/openapi/v1.json';
const out = resolve(import.meta.dirname, '..', 'src', 'api', 'schema.d.ts');
const args = ['--yes', '-p', 'openapi-typescript@7.13.0', '-p', 'typescript@5.9.3', 'openapi-typescript', source];

// npx is a .cmd on Windows, which needs a shell; quote the arguments for it
const quoted = process.platform === 'win32' ? args.map((a) => (/[\s"&|<>^]/.test(a) ? `"${a.replace(/"/g, '""')}"` : a)) : args;
const result = spawnSync('npx', quoted, { shell: process.platform === 'win32', encoding: 'utf8', maxBuffer: 32 * 1024 * 1024 });
if (result.status !== 0 || !result.stdout.includes('export interface components')) {
  process.stderr.write(result.stderr || result.stdout || 'openapi-typescript failed\n');
  process.exit(result.status || 1);
}
writeFileSync(out, result.stdout);
console.log(`Wrote ${out} from ${source}`);
