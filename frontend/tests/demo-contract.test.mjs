import assert from 'node:assert/strict';
import { test } from 'node:test';
import { mkdtemp, readFile, writeFile, mkdir, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import ts from 'typescript';

// Execute the actual adapter in isolation, with fresh module state per scenario.
async function scenario(run) {
  const dir = await mkdtemp(join(tmpdir(), 'pulselink-demo-'));
  const original = Object.getOwnPropertyDescriptor(globalThis, 'localStorage');
  const values = new Map();
  const storage = {
    getItem: key => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
    removeItem: key => values.delete(key),
  };
  Object.defineProperty(globalThis, 'localStorage', { value: storage, configurable: true });
  try {
    for (const file of ['api/limits', 'api/errors', 'demo/fixture', 'demo/statusMachine', 'demo/store', 'demo/demoApi']) {
      const source = await readFile(new URL(`../src/${file}.ts`, import.meta.url), 'utf8');
      const compiled = ts.transpileModule(source, {
        compilerOptions: { target: ts.ScriptTarget.ES2023, module: ts.ModuleKind.ESNext },
      }).outputText.replace(/from '(\.[^']+)'/g, "from '$1.mjs'");
      const destination = join(dir, `${file}.mjs`);
      await mkdir(join(destination, '..'), { recursive: true });
      await writeFile(destination, compiled);
    }
    const { demoApi } = await import(pathToFileURL(join(dir, 'demo/demoApi.mjs')));
    const store = await import(pathToFileURL(join(dir, 'demo/store.mjs')));
    storage.setItem('pulselink.auth', JSON.stringify(await demoApi.login('paramedic@pulselink.demo', 'demo')));
    await run(demoApi, store, storage);
  } finally {
    if (original) Object.defineProperty(globalThis, 'localStorage', original);
    else delete globalThis.localStorage;
    await rm(dir, { recursive: true, force: true });
  }
}

test('Replay trims names, preserves optional values, and never repeats the mutation', () => scenario(async api => {
  const incident = (await api.incidents()).items.find(i => i.status === 'Draft');
  const key = crypto.randomUUID();
  const result = await api.addIntervention(incident.id, { name: '  Oxygen  ', notes: '' }, incident.version, key);
  assert.deepEqual(await api.addIntervention(incident.id, { name: 'Oxygen', notes: '' }, incident.version, key), result);
  for (const field of ['medication', 'dose', 'route', 'notes']) {
    const body = { name: 'Oxygen', notes: '', [field]: field === 'notes' ? null : '' };
    await assert.rejects(api.addIntervention(incident.id, body, incident.version, key), error => error.status === 409);
  }
  const detail = await api.incident(incident.id);
  assert.equal(detail.interventions.length, incident.interventions.length + 1);
  assert.equal(detail.interventions.at(-1).notes, '');
  assert.equal(detail.interventions.at(-1).medication, null);
  assert.equal(detail.auditEvents.length, incident.auditEvents.length + 1);
}));

test('Hospital receipt persists for EMS and preserves the original on retry', () => scenario(async (api, _store, storage) => {
  const medic = storage.getItem('pulselink.auth');
  const hospital = await api.login('hospital@pulselink.demo', 'demo');
  const handedOff = (await api.incidents()).items.find(i => i.status === 'HandedOff' && i.destinationHospitalId === hospital.hospitalId);
  await assert.rejects(api.acknowledgeReceipt(handedOff.id), e => e.status === 403);
  storage.setItem('pulselink.auth', JSON.stringify({ ...hospital, hospitalId: 'another-hospital' }));
  await assert.rejects(api.acknowledgeReceipt(handedOff.id), e => e.status === 403);
  storage.setItem('pulselink.auth', JSON.stringify(hospital));
  const pending = (await api.incidents()).items.find(i => i.status !== 'HandedOff');
  await assert.rejects(api.acknowledgeReceipt(pending.id), e => e.status === 409);
  const receipt = await api.acknowledgeReceipt(handedOff.id);
  assert.deepEqual(await api.acknowledgeReceipt(handedOff.id), receipt);
  storage.setItem('pulselink.auth', medic);
  const detail = await api.incident(handedOff.id);
  assert.equal(detail.version, handedOff.version);
  assert.deepEqual(detail.auditEvents.filter(a => a.action === 'HandoffAcknowledged'), [receipt]);
  assert.ok(receipt.details.includes(hospital.displayName));
}));

for (const failure of ['getItem', 'setItem']) {
  test(`Incident storage survives ${failure} failure and reset`, () => scenario(async (api, store, storage) => {
    const incident = (await api.incidents()).items.find(i => i.status === 'Draft');
    storage[failure] = key => {
      if (key === 'pulselink.demo.state') throw new Error('Storage unavailable');
      return null;
    };
    // Exercise the store directly: authentication storage is a separate concern.
    const state = store.loadState();
    state.incidents.find(i => i.id === incident.id).notes = 'Retained draft';
    store.saveState(state);
    assert.equal(store.loadState().incidents.find(i => i.id === incident.id).notes, 'Retained draft');
    const discarded = store.loadState();
    discarded.incidents[0].notes = 'Uncommitted';
    assert.notEqual(store.loadState().incidents[0].notes, 'Uncommitted');
    store.resetDemo();
    assert.notEqual(store.loadState().incidents.find(i => i.id === incident.id).notes, 'Retained draft');
  }));
}

test('Healthy storage sees another tab and keeps its last read if storage later fails', () => scenario(async (_api, store, storage) => {
  const external = store.loadState();
  external.incidents[0].notes = 'Written by another tab';
  storage.setItem('pulselink.demo.state', JSON.stringify(external));
  assert.equal(store.loadState().incidents[0].notes, 'Written by another tab');
  storage.getItem = () => { throw new Error('Storage unavailable'); };
  assert.equal(store.loadState().incidents[0].notes, 'Written by another tab');
}));


test('Narrative boundary is enforced without mutating demo state', () => scenario(async api => {
  const body = { chiefComplaint: 'Narrative boundary', notes: 'n'.repeat(2000) };
  const created = await api.createIncident(body);
  assert.equal(created.notes, body.notes);
  const before = await api.incident(created.id);
  await assert.rejects(api.updateIncident(created.id, { ...body, notes: body.notes + 'n' }, before.version), e => e.status === 400);
  assert.deepEqual(await api.incident(created.id), before);
  const count = (await api.incidents()).totalCount;
  await assert.rejects(api.createIncident({ ...body, notes: body.notes + 'n' }), e => e.status === 400);
  assert.equal((await api.incidents()).totalCount, count);
  const updated = await api.updateIncident(created.id, { ...body, notes: 'x'.repeat(2000) }, before.version);
  assert.equal(updated.notes, 'x'.repeat(2000));
}));
