# Task 4.2 — BPMN smoke test (throwaway Zeebe)

Validates that the two diagrams in `../bpmn` deploy to Camunda 8 (Zeebe) and
start instances correctly — **without** any .NET workers or domain services.

What it proves:
- `payment-saga.bpmn` deploys (engine model validation OK) and, after publishing
  `START_PAYMENT`, a `PaymentSaga` instance starts and reaches the
  `CREATE_PAYMENT` job.
- `cancel-payment.bpmn` deploys and, after publishing `CANCEL_PAYMENT`, a
  `CancelPayment` instance starts and reaches the `RETURN_FUNDS` job.

> Jobs are *not* completed here (no workers). Seeing a job **created/activated**
> is the expected positive result — a model/deploy error would fail earlier.

## Files
| file | purpose |
|---|---|
| `docker-compose.zeebe-test.yml` | throwaway single-node Zeebe engine (gateway `:26500`, network `zeebe-test-net`) |
| `deploy.bat` | starts the test containers in Docker Desktop |
| `run_test.bat` | runs `zbctl` (from the Zeebe image) to deploy + start instances + assert |

## Pre-requisites
- Docker Desktop running.
- `../bpmn/*.bpmn` present (they are).

## Usage
```cmd
cd Task4.2\tests
deploy.bat     rem starts the Zeebe container in Docker Desktop
run_test.bat   rem installs local zbctl (npm) if needed, deploys the BPMN,
               rem publishes start messages and asserts the jobs appear
```

Teardown (removes containers + volumes):
```cmd
docker compose -f docker-compose.zeebe-test.yml down -v
```

## Notes / troubleshooting
- **`zbctl` is installed locally via npm** (`npm install zbctl@8.6.0` in this
  folder) the first time `run_test.bat` runs — no global install / download is
  needed. `node_modules/` is git-ignored; `package-lock.json` pins the version.
- `run_test.bat` **must use `call` before every `zbctl` invocation**, because
  `zbctl.cmd` is itself a `.cmd` file — invoking it without `call` would end the
  batch after the first call (classic `cmd.exe` gotcha).
- The batch files must have **CRLF** line endings (`cmd` misbehaves with LF-only
  files when using `goto`).
- `run_test.bat` waits for the gateway (`localhost:26500`) to become reachable
  before deploying. Run `deploy.bat` first.
- The test **deploys** both diagrams and **starts** instances by publishing
  their start messages, then asserts the first job appears:
  `START_PAYMENT → CREATE_PAYMENT`, `CANCEL_PAYMENT → RETURN_FUNDS`.
  Jobs are not completed (no workers), which is the expected result here.
- No Elasticsearch is started (the ES exporter is disabled) — enough for
  deploy/start validation. Enable ES only if you later attach Operate.