#!/usr/bin/env node
/**
 * Fails the build when a translation is missing.
 *
 * Catches the two ways this has broken before:
 *  - a key used in a template that was only added to en.json
 *  - a key added to one locale but forgotten in the others
 *
 * Dynamic keys (e.g. `'about.value' + k + 'Text'`) can't be resolved statically
 * and are skipped; locale parity still covers them.
 */
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const srcDir = join(root, 'src');
const i18nDir = join(root, 'public', 'i18n');
const langs = ['en', 'nl', 'ar'];

const walk = dir =>
  readdirSync(dir).flatMap(name => {
    const full = join(dir, name);
    return statSync(full).isDirectory() ? walk(full) : [full];
  });

const flatten = (obj, prefix = '') =>
  Object.entries(obj).reduce((acc, [k, v]) => {
    const key = prefix + k;
    if (v && typeof v === 'object') Object.assign(acc, flatten(v, key + '.'));
    else acc[key] = v;
    return acc;
  }, {});

const locales = Object.fromEntries(
  langs.map(lang => [lang, flatten(JSON.parse(readFileSync(join(i18nDir, `${lang}.json`), 'utf8')))])
);

// Keys referenced statically in templates and components.
const used = new Set();
const patterns = [
  /'([a-zA-Z0-9_.]+)'\s*\|\s*transloco/g,
  /\btranslate\(\s*'([a-zA-Z0-9_.]+)'/g,
  /\bselectTranslate\(\s*'([a-zA-Z0-9_.]+)'/g
];

for (const file of walk(srcDir).filter(f => /\.(ts|html)$/.test(f))) {
  const text = readFileSync(file, 'utf8');
  for (const pattern of patterns) {
    for (const match of text.matchAll(pattern)) {
      // skip fragments of concatenated dynamic keys
      if (match[1].includes('.')) used.add(match[1]);
    }
  }
}

const errors = [];

for (const lang of langs) {
  const missing = [...used].filter(key => !(key in locales[lang])).sort();
  if (missing.length) errors.push(`${lang}.json missing keys used in code:\n    ${missing.join('\n    ')}`);
}

const allKeys = new Set(langs.flatMap(lang => Object.keys(locales[lang])));
for (const lang of langs) {
  const gaps = [...allKeys].filter(key => !(key in locales[lang])).sort();
  if (gaps.length) errors.push(`${lang}.json is missing keys other locales define:\n    ${gaps.join('\n    ')}`);
}

for (const lang of langs) {
  const empty = Object.entries(locales[lang])
    .filter(([, v]) => typeof v !== 'string' || !v.trim())
    .map(([k]) => k);
  if (empty.length) errors.push(`${lang}.json has empty values:\n    ${empty.join('\n    ')}`);
}

if (errors.length) {
  console.error('\ni18n check failed:\n');
  for (const error of errors) console.error('  - ' + error + '\n');
  process.exit(1);
}

console.log(`i18n check passed: ${Object.keys(locales.en).length} keys x ${langs.length} locales, ${used.size} referenced in code.`);
