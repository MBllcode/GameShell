import assert from 'node:assert/strict';
import fs from 'node:fs';
import test from 'node:test';

const sourceRoot = new URL('../src/', import.meta.url);

function collectSourceFiles(directory: URL): URL[] {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const entryUrl = new URL(entry.name, directory);
    if (entry.isDirectory()) {
      return collectSourceFiles(new URL(`${entry.name}/`, directory));
    }

    return /\.(ts|tsx|css|html|json)$/.test(entry.name) ? [entryUrl] : [];
  });
}

const requiredPolishCopy = [
  'Zachowanie profilu',
  'Skróty profilu',
  'Zapisz zmiany',
  'Panel główny',
  'Przywróć otwarte okna',
  'Przenieś launcher na pierwszy plan',
  'Uruchamianie',
];

test('frontend Polish copy remains UTF-8 and free of mojibake', () => {
  const source = collectSourceFiles(sourceRoot)
    .map((file) => fs.readFileSync(file, 'utf8'))
    .join('\n');

  for (const text of requiredPolishCopy) {
    assert.ok(source.includes(text), `Missing expected Polish copy: ${text}`);
  }

  for (const marker of ['Ã', 'Â', 'Ä', 'Å', 'â', '�']) {
    assert.equal(source.includes(marker), false, `Found mojibake marker: ${marker}`);
  }
});