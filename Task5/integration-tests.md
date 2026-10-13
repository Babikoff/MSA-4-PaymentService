# Интеграционные тесты #

| **№** | **Название теста** | **Тип** | **Компоненты** | **Предусловия** |
| :-: | --- | --- | --- | --- |
| 01 | `bpmn.smoke.start-payment`</br>Успешное создание платежа | Integration | START_PAYMENT -> CREATE_PAYMENT job | payment-saga.bpmn задеплоено |
| 02 | `bpmn.smoke.cancel-payment`</br>Успешная отмена платежа | Integration | CANCEL_PAYMENT -> RETURN_FUNDS job | cancel-payment.bpmn задеплоено |
| 03 | `bpmn.workflow.happy`</br>Успешное проведение платежа | End-to-end | CREATE -> HOLD -> AUTOCHECK -> TRANSFER -> COMPLETE -> NOTIFY (Успешный платёж) | Баланс > 0 |
| 04 | `bpmn.workflow.holdfail`</br>Ошибка при резервировании | End-to-end | CREATE -> HOLD -> NOTIFY (Ошибка резервирования) | Баланс < Суммы платежа |
| 05 | `bpmn.workflow.block`</br>Успешное проведение платежа с авто-антифродом | End-to-end | CREATE -> HOLD -> AUTOCHECK -> SECURITY -> RELEASE -> FINISH -> NOTIFY (Платёж заблокирован) | Баланс > 0, признак фрода |
| 06 | `bpmn.workflow.manual`</br>Успешное проведение платежа с ручным антифродом | End-to-end | CREATE -> HOLD -> AUTOCHECK -> MANUAL_CHECK -> TRANSFER -> COMPLETE -> NOTIFY | Баланс > 0, нет признаков фрода |
| 07 | `bpmn.workflow.transferfail`</br>Ошибка при переводе средств | End-to-end | CREATE -> HOLD -> AUTOCHECK -> TRANSFER -> RETURN -> FINISH -> NOTIFY (Ошибка платежа) | Баланс > 0, эмуляция сетевой ошибки |
| 08 | `bpmn.workflow.cancelhold`</br>Отмена резервирования средств | End-to-end | CREATE -> HOLD -> CANCEL_HOLD -> RELEASE -> FINISH -> NOTIFY (Успешная отмена) | Баланс > 0 |
| 09 | `bpmn.workflow.cancelref`</br>Отмена платежа | End-to-end | CANCEL_PAYMENT -> RETURN -> FINISH -> NOTIFY | Успешное проведение платежа ранее |
