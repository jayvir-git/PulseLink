import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { test } from 'node:test';

// The demo runs in the browser, so its copy of the status machine is TypeScript
// while the authority is C#. Duplication is the price of a serverless demo; this
// test is what stops the two drifting apart silently, which would leave the demo
// showing behaviour the product does not have.
const root = fileURLToPath(new URL('../../', import.meta.url));
const csharp = readFileSync(root + 'backend/PulseLink.Core/Domain/IncidentStatusMachine.cs', 'utf8');
const typescript = readFileSync(root + 'frontend/src/demo/statusMachine.ts', 'utf8');

function parseCsharpTransitions(source) {
  const block = source.slice(source.indexOf('new()'), source.indexOf('public static bool CanTransition'));
  const table = {};
  for (const line of block.split('\n')) {
    const entry = line.match(/\[IncidentStatus\.(\w+)\]\s*=\s*\[([^\]]*)\]/);
    if (!entry) continue;
    const targets = [...entry[2].matchAll(/IncidentStatus\.(\w+)/g)].map(m => m[1]);
    table[entry[1]] = targets;
  }
  return table;
}

function parseTypescriptTransitions(source) {
  const start = source.indexOf('ALLOWED_TRANSITIONS');
  const block = source.slice(start, source.indexOf('};', start));
  const table = {};
  for (const line of block.split('\n')) {
    const entry = line.match(/^\s*(\w+):\s*\[([^\]]*)\]/);
    if (!entry) continue;
    const targets = [...entry[2].matchAll(/'(\w+)'/g)].map(m => m[1]);
    table[entry[1]] = targets;
  }
  return table;
}

test('The demo status machine matches IncidentStatusMachine', () => {
  const expected = parseCsharpTransitions(csharp);
  const actual = parseTypescriptTransitions(typescript);
  // Guard the parsers themselves: an empty table would make this test vacuous.
  assert.equal(Object.keys(expected).length, 6, 'parsed the C# transition table');
  assert.deepEqual(actual, expected);
});

test('The demo destination rule matches EnsureValidState', () => {
  const csharpRule = csharp.slice(csharp.indexOf('EnsureValidState(IncidentStatus status'));
  const expected = [...csharpRule.matchAll(/IncidentStatus\.(\w+)/g)].map(m => m[1]);
  const block = typescript.slice(typescript.indexOf('DESTINATION_REQUIRED_FROM'));
  const actual = [...block.slice(0, block.indexOf(';')).matchAll(/'(\w+)'/g)].map(m => m[1]);
  assert.equal(expected.length, 3, 'parsed the C# destination rule');
  assert.deepEqual(actual, expected);
});
