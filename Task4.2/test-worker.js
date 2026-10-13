'use strict';
/**
 * test-worker.js - stub orchestrator for BPMN orchestration tests (Task4.2).
 * Заменяет настоящий оркестратор: активирует джобы payment-saga.bpmn,
 * завершает их переменными по сценарию, фиксирует последовательности
 * и пишет вердикты в results.txt для run-bpmn-tests.bat.
 *
 * Использование:  npm i zeebe-node   (в Task4.2)
 *                 node test-worker.js
 * Сигналы: worker.ready, worker.pid, jobs.log, results.txt, hold-active.txt
 */
const fs = require('fs');
const path = require('path');
const os = require('os');
const { ZBClient } = require('zeebe-node');

// ---- Config ----
const HOST = process.env.ZEEBE_HOST || 'localhost';
const PORT = Number(process.env.ZEEBE_PORT || 26500);
const OUT = (f) => path.join(__dirname, f);
// Держим "открытые" джобы дольше прогона, чтобы .bat успел опубликовать CANCEL_HOLD.
const JOB_TIMEOUT_MS = Number(process.env.JOB_TIMEOUT_MS || 120000);
const MAX_RUNTIME_MS = Number(process.env.MAX_RUNTIME_MS || 600000);
const EXPECT_VERDICTS = Number(process.env.EXPECT_VERDICTS || 7);
// 0 = без искусственной задержки; можно поднять для стресс-теста latency.
const JOB_DELAY_MS = Number(process.env.JOB_DELAY_MS || 0);

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const append = (file, line) =>
  fs.appendFileSync(OUT(file), line + os.EOL); // 0.1-2 ms, маленькие записи

// ---- Job types из payment-saga.bpmn (11 шт.) ----
const JOB_TYPES = [
  'CREATE_PAYMENT', 'HOLD_FUNDS', 'RELEASE_FUNDS',
  'ANTIFRAUD_AUTOCHECK', 'ANTIFRAUD_MANUAL_CHECK',
  'TRANSFER_FUNDS', 'RETURN_FUNDS', 'COMPLETE_PAYMENT',
  'FINISH_PAYMENT_PROCESS', 'SEND_NOTIFICATION_TO_USER',
  'SEND_NOTIFICATION_TO_SECURITY',
];

// ---- Сценарии: выходные переменные по сценарию ----
const SCENARIOS = {
  happy:        { holdOk: true,  fraud: 'ALLOW',  transferOk: true  },
  holdfail:     { holdOk: false, fraud: 'ALLOW',  transferOk: true  },
  block:        { holdOk: true,  fraud: 'BLOCK',  transferOk: true  },
  manual:       { holdOk: true,  fraud: 'MANUAL', manualFraud: 'ALLOW', transferOk: true },
  transferfail: { holdOk: true,  fraud: 'ALLOW',  transferOk: false },
  cancelhold:   { holdOk: true,  fraud: 'ALLOW',  transferOk: true  }, // HOLD не завершаем
  cancelref:    { holdOk: true,  fraud: 'ALLOW',  transferOk: true  },
  timeout:      { holdOk: true,  fraud: 'MANUAL', manualFraud: null, transferOk: true }, // PT5M
};

// ---- Ожидаемые последовательности (из payment-saga.bpmn) ----
const EXPECTED = {
  happy:        ['CREATE_PAYMENT', 'HOLD_FUNDS', 'ANTIFRAUD_AUTOCHECK', 'TRANSFER_FUNDS', 'COMPLETE_PAYMENT', 'SEND_NOTIFICATION_TO_USER'],
  holdfail:     ['CREATE_PAYMENT', 'HOLD_FUNDS', 'SEND_NOTIFICATION_TO_USER'],
  block:        ['CREATE_PAYMENT', 'HOLD_FUNDS', 'ANTIFRAUD_AUTOCHECK', 'SEND_NOTIFICATION_TO_SECURITY', 'RELEASE_FUNDS', 'FINISH_PAYMENT_PROCESS', 'SEND_NOTIFICATION_TO_USER'],
  manual:       ['CREATE_PAYMENT', 'HOLD_FUNDS', 'ANTIFRAUD_AUTOCHECK', 'ANTIFRAUD_MANUAL_CHECK', 'TRANSFER_FUNDS', 'COMPLETE_PAYMENT', 'SEND_NOTIFICATION_TO_USER'],
  transferfail: ['CREATE_PAYMENT', 'HOLD_FUNDS', 'ANTIFRAUD_AUTOCHECK', 'TRANSFER_FUNDS', 'RETURN_FUNDS', 'FINISH_PAYMENT_PROCESS', 'SEND_NOTIFICATION_TO_USER'],
  cancelhold:   ['CREATE_PAYMENT', 'HOLD_FUNDS', 'RELEASE_FUNDS', 'FINISH_PAYMENT_PROCESS', 'SEND_NOTIFICATION_TO_USER'],
  cancelref:    ['RETURN_FUNDS', 'FINISH_PAYMENT_PROCESS', 'SEND_NOTIFICATION_TO_USER'],
  timeout:      ['CREATE_PAYMENT', 'HOLD_FUNDS', 'ANTIFRAUD_AUTOCHECK', 'ANTIFRAUD_MANUAL_CHECK', 'TRANSFER_FUNDS', 'COMPLETE_PAYMENT', 'SEND_NOTIFICATION_TO_USER'],
};

// ---- Runtime state ----
const seq = new Map();          // paymentId -> [jobType, ...]
const verdictDone = new Set();  // paymentId с уже записанным вердиктом
const heldKeys = new Set();     // ключи джоб, которые держим открытыми (дедуп ретраев)
let passed = 0, failed = 0;

function buildVars(type, cfg) {
  switch (type) {
    case 'HOLD_FUNDS':             return { holdOk: cfg.holdOk };
    case 'ANTIFRAUD_AUTOCHECK':    return { fraudDecision: cfg.fraud };
    case 'ANTIFRAUD_MANUAL_CHECK': return { fraudDecision: cfg.manualFraud || 'ALLOW' };
    case 'TRANSFER_FUNDS':         return { transferOk: cfg.transferOk };
    default:                       return {};
  }
}

function record(type, pid, scenario) {
  append('jobs.log', `${pid}|${type}`);
  const arr = seq.get(pid) || [];
  arr.push(type);
  seq.set(pid, arr);

  const expected = EXPECTED[scenario];
  if (!expected || verdictDone.has(pid)) return;
  const idx = arr.length - 1;
  if (expected[idx] !== type) {                     // ветка пошла не туда
    verdictDone.add(pid); failed++;
    append('results.txt',
      `verdict|${scenario}|${pid}|FAIL|got=${arr.join('>')}|expected=${expected.join('>')}`);
  } else if (arr.length === expected.length) {      // цепочка сошлась
    verdictDone.add(pid); passed++;
    append('results.txt', `verdict|${scenario}|${pid}|PASS|seq=${arr.join('>')}`);
  }
  if (passed + failed >= EXPECT_VERDICTS) done();
}

async function handler(type, job) {
  const vars = job.variables || {};
  const pid = String(vars.paymentId || 'unknown');
  const scenario = vars.scenario || 'happy';
  const cfg = SCENARIOS[scenario] || SCENARIOS.happy;

  // Ретрай висящего джоба (после job timeout) - не логируем повторно.
  if (heldKeys.has(job.key)) return; // держим открытым: complete() не вызываем

  record(type, pid, scenario);
  if (JOB_DELAY_MS > 0) await sleep(JOB_DELAY_MS); // эмуляция latency сервисов

  // Не завершаем: ждём, пока .bat пришлёт CANCEL_HOLD / сработает PT5M.
  if (type === 'HOLD_FUNDS' && scenario === 'cancelhold') {
    heldKeys.add(job.key);
    append('hold-active.txt', pid);
    return; // complete() не вызываем - джоб висит до timeout
  }
  if (type === 'ANTIFRAUD_MANUAL_CHECK' && scenario === 'timeout') {
    heldKeys.add(job.key);
    return;
  }
  // zeebe-node@8: возвращаемое значение игнорируется - завершаем явно.
  job.complete(buildVars(type, cfg));
}

function done(code) {
  console.log(`WORKER DONE pass=${passed} fail=${failed}`);
  try { fs.unlinkSync(OUT('worker.ready')); } catch (_) { /* already gone */ }
  process.exit(code !== undefined ? code : (failed > 0 ? 1 : 0));
}

async function main() {
  // Логи прошлого прогона не должны мешать новому.
  for (const f of ['jobs.log', 'results.txt', 'hold-active.txt', 'worker.ready']) {
    try { fs.unlinkSync(OUT(f)); } catch (_) { /* not exists */ }
  }

  const zbc = new ZBClient({ host: HOST, port: PORT }); // plaintext (local :26500)
  for (const type of JOB_TYPES) {
    zbc.createWorker({
      taskType: type,
      taskHandler: (job) => handler(type, job),
      maxJobsToActivate: 10,
      timeout: JOB_TIMEOUT_MS,
    });
  }
  fs.writeFileSync(OUT('worker.pid'), String(process.pid));
  fs.writeFileSync(OUT('worker.ready'), new Date().toISOString());
  console.log('WORKER READY'); // сигнал для run-bpmn-tests.bat

  setTimeout(() => { console.error('WORKER TIMEOUT'); done(2); }, MAX_RUNTIME_MS);
  const stop = () => done(0);
  process.on('SIGINT', stop);
  process.on('SIGTERM', stop);
}

main().catch((e) => { console.error(e); process.exit(3); });

