import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const licenseRoot = resolve(root, 'docs/licenses');
const components = JSON.parse(readFileSync(resolve(licenseRoot, 'components.json'), 'utf8'));
const sections = ['GameShell\n\n' + readFileSync(resolve(root, 'LICENSE'), 'utf8')];
for (const component of components) {
  const texts = component.files.map(file => {
    if (file.includes('/') || file.includes('\\') || !file.endsWith('.txt')) {
      throw new Error(`Invalid notice filename: ${file}`);
    }
    return readFileSync(resolve(licenseRoot, file), 'utf8');
  });
  sections.push(`${component.name} — ${component.version}\n${component.source}\n\n${texts.join('\n\n')}`);
}
sections.push('Product names and logos\n\nPlaynite, Steam and Xbox names and logos identify optional third-party applications. They belong to their respective owners and are not covered by the GameShell MIT license. GameShell is not affiliated with or endorsed by these projects or companies.');
const notices = sections.join('\n\n' + '='.repeat(72) + '\n\n') + '\n';
const escaped = notices.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
const publicRoot = resolve(root, 'src/GameShell.Ui/public');
const artifactRoot = resolve(root, 'artifacts/licenses');
mkdirSync(artifactRoot, { recursive: true });
writeFileSync(resolve(artifactRoot, 'THIRD_PARTY_NOTICES.txt'), notices);
writeFileSync(resolve(publicRoot, 'licenses.html'), `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'">
<title>GameShell — licenses</title><style>
:root { color-scheme: dark; font: 14px/1.6 system-ui, sans-serif; background: #151a17; color: #f2f5f2; }
body { margin: 20px; } pre { white-space: pre-wrap; overflow-wrap: anywhere; font: inherit; }
</style></head><body><h1>Licenses and third-party notices</h1><pre>${escaped}</pre></body></html>\n`);
console.log(`Prepared offline license notices for ${components.length} components.`);
