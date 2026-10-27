import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const root = new URL('../../../', import.meta.url);
const read = (path: string) => readFileSync(new URL(path, root), 'utf8');
const components = JSON.parse(read('docs/licenses/components.json')) as Array<{ name: string; version: string; files: string[] }>;

test('notices cover every resolved production npm dependency at its locked version', () => {
  const lock = JSON.parse(read('src/GameShell.Ui/package-lock.json'));
  for (const [path, value] of Object.entries(lock.packages) as Array<[string, { dev?: boolean; version: string }]>) {
    if (!path || value.dev) continue;
    const component = components.find(entry => entry.name === path.replace(/^node_modules\//, ''));
    assert.equal(component?.version, value.version, `Update notices for ${path}`);
  }
});

test('offline notices include complete vendored texts and the application license', () => {
  const output = read('artifacts/licenses/THIRD_PARTY_NOTICES.txt');
  assert.ok(output.includes(read('LICENSE')));
  for (const component of components) {
    assert.ok(component.files.length > 0, component.name);
    for (const file of component.files) assert.ok(output.includes(read(`docs/licenses/${file}`)), file);
  }
  const html = read('src/GameShell.Ui/public/licenses.html');
  assert.ok(html.includes('Content-Security-Policy'));
  assert.ok(html.includes('SIL OPEN FONT LICENSE'));
});
