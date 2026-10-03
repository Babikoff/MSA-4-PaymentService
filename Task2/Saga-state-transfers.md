# Переходы Saga #

|**Исходное состояние**|**Переходное состояние**|**Событие**|
| :-: | :- | :- |
| - | Процесс платежа запущен</br>PAYMENT_STARTED | CREATE_PAYMENT |
| Процесс платежа запущен</br>PAYMENT_STARTED | Средства зарезервированны</br>FUNDS_HELD | HOLD_FUNDS |
| Средства зарезервированны</br>FUNDS_HELD | Резервирование средств отменено</br>FUNDS_RELEASED | CANCEL_PAYMENT |
| Средства зарезервированны</br>FUNDS_HELD | Резервирование средств отменено</br>FUNDS_RELEASED | RELEASE_FUNDS |
| Резервирование средств отменено</br>FUNDS_RELEASED | Процесс оплаты отменён плательщиком</br>PAYMENT_PROCESS_CANCELED | ? |
| Средства зарезервированы</br>FUNDS_HELD | Легальность операции проверена</br>ANTIFRAUD_CHECKED | ANTIFRAUD_AUTOCHECK |
| Средства зарезервированы</br>FUNDS_HELD | Легальность операции проверена</br>ANTIFRAUD_CHECKED | ANTIFRAUD_MANUAL_CHECK |
| Легальность операции проверена</br>ANTIFRAUD_CHECKED | Средства переведены</br>FUNDS_TRANSFERED | TRANSFER_FUNDS |
| Средства зарезервированы</br>FUNDS_HELD | Легальность операции проверена</br>FRAUD_OPERATION_DETECTED | ANTIFRAUD_AUTOCHECK |
| Средства зарезервированы</br>FUNDS_HELD | Легальность операции проверена</br>FRAUD_OPERATION_DETECTED | ANTIFRAUD_MANUAL_CHECK |
| Средства зарезервированы</br>FUNDS_HELD | Легальность операции проверена</br>ANTIFRAUD_CHECKED/FRAUD_OPERATION_DETECTED | ANTIFRAUD_TIMEOUT |
| Смена статуса процесса</br>PROCESS_STATUS_CHANGED | Плательщик уведомлён</br>PAYER_NOTIFIED | SEND_NOTIFICATION_TO_USER |
| Легальность операции проверена (операция определена как подозрительная)</br>FRAUD_OPERATION_DETECTED | Служба безопасности уведомлена</br>SECURITY_NOTIFIED | SEND_NOTIFICATION_TO_SECURITY |
| Легальность операции проверена (операция определена как подозрительная)</br>FRAUD_OPERATION_DETECTED | FUNDS_RELEASED | RELEASE_FUNDS |
| Средства переведены</br>FUNDS_TRANSFERED | Процесс платежа завершен</br>PAYMENT_PROCESS_COMPLETED | COMPLETE_PAYMENT |
| Cредства переведены</br>FUNDS_TRANSFERED | Процесс оплаты отменён плательщиком</br>PAYMENT_PROCESS_CANCELED | CANCEL_PAYMENT |
| Процесс оплаты отменён плательщиком</br>PAYMENT_PROCESS_CANCELED | Средства возвращены</br>FUNDS_RETURNED | RETURN_FUNDS |
| FUNDS_RELEASED | PAYMENT_PROCESS_FINISHED | FINISH_PAYMENT_PROCESS |
| FUNDS_RETURNED | PAYMENT_PROCESS_FINISHED | FINISH_PAYMENT_PROCESS |
| FUNDS_TRANSFERED | PAYMENT_PROCESS_FINISHED | FINISH_PAYMENT_PROCESS |
