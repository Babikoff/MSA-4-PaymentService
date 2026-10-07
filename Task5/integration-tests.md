# Интеграционные тесты #

| **№** | **Название теста** | **Тип** | **Компоненты** | **Предусловия** |
| :-: | --- | --- | --- | --- |
| 1 | `smoke.docker-health` | Health-check | Docker | Docker запущен |
| 2 | `smoke.services-up` | Service-up | zeebe, elasticsearch, operate, tasklist, postgres, deploy-process | Все контейнеры запущены |
| 3 | `bpmn.zeebe-gateway` | Health-check | Zeebe gateway :26500 | Zeebe доступен |
| 4 | `bpmn.deploy-files` | Deploy | deploy-process контейнер | deploy-process отработал |
| 5 | `bpmn.deploy-payment-saga` | Deploy-verify | PaymentSaga в логах deploy-process | Деплой выполнен |
| 6 | `bpmn.deploy-cancel-payment` | Deploy-verify | CancelPayment в логах deploy-process | Деплой выполнен |
| 7 | `bpmn.proc-deploy-success` | Deploy-verify | "Processes deployed" в логах deploy-process | deploy-process успешен |
| 8 | `bpmn.operate-health` | Health-check | Operate :8081 | HTTP 200 |
| 9 | `bpmn.tasklist-health` | Health-check | Tasklist :8082 | HTTP 200 |
| 10 | `bpmn.smoke.all-deploy` | Deploy | payment-saga.bpmn + cancel-payment.bpmn | Zeebe :26500, zbctl, worker запущен |
| 11 | `bpmn.smoke.start-payment` | Integration | START_PAYMENT -> CREATE_PAYMENT job | payment-saga.bpmn задеплоено |
| 12 | `bpmn.smoke.cancel-payment` | Integration | CANCEL_PAYMENT -> RETURN_FUNDS job | cancel-payment.bpmn задеплоено |
| 13 | `bpmn.workflow.happy` | End-to-end | CREATE -> HOLD -> AUTOCHECK -> TRANSFER -> COMPLETE -> NOTIFY (ALLOW) | test-worker.js запущен |
| 14 | `bpmn.workflow.holdfail` | End-to-end | CREATE -> HOLD -> NOTIFY (holdOk=false) | test-worker.js запущен |
| 15 | `bpmn.workflow.block` | End-to-end | CREATE -> HOLD -> AUTOCHECK -> SECURITY -> RELEASE -> FINISH -> NOTIFY (BLOCK) | test-worker.js запущен |
| 16 | `bpmn.workflow.manual` | End-to-end | CREATE -> HOLD -> AUTOCHECK(MANUAL) -> MANUAL_CHECK -> TRANSFER -> COMPLETE -> NOTIFY | test-worker.js запущен |
| 17 | `bpmn.workflow.transferfail` | End-to-end | CREATE -> HOLD -> AUTOCHECK -> TRANSFER -> RETURN -> FINISH -> NOTIFY (transferOk=false) | test-worker.js запущен |
| 18 | `bpmn.workflow.cancelhold` | End-to-end | CREATE -> HOLD(вічний)  -> CANCEL_HOLD  -> RELEASE -> FINISH -> NOTIFY | test-worker.js запущен |
| 19 | `bpmn.workflow.cancelref` | End-to-end | CANCEL_PAYMENT -> RETURN -> FINISH -> NOTIFY (cancel-payment.bpmn) | test-worker.js запущен |
| 20 | `http.payment.crud` | HTTP | PaymentService CRUD + валидация | payment-service запущен |
| 21 | `http.payment.chain` | HTTP | hold -> transfer -> complete (цепочка) | payment-service запущен |
| 22 | `http.payment.compensate` | HTTP | release и return (компенсации) | payment-service запущен |
| 23 | `http.fraudcheck` | HTTP | Авто/ручная проверка, решение оператора, 404/409/400 | fraud-check-service запущен |
| 24 | `http.notification` | HTTP | notify user/security, лог-чеки | notification-service запущен |
| 25 | `db.payment` | SQL | paymentdb: Payments/OperationRecords, idempotency-count | PostgreSQL запущен |
| 26 | `db.fraudcheck` | SQL | fraudcheckdb: Payments/OperationRecords(final-status) | PostgreSQL запущен |
