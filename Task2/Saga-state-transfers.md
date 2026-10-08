# Переходы Saga #

|**Исходное состояние**|**Переходное состояние**|**Событие**|
| :-: | :- | :- |
| - | Процесс платежа запущен</br>PAYMENT_STARTED | CREATE_PAYMENT |
| Процесс платежа запущен</br>PAYMENT_STARTED | Средства зарезервированы</br>FUNDS_HELD | HOLD_FUNDS |
| Средства зарезервированы</br>FUNDS_HELD | Резервирование средств отменено</br>FUNDS_RELEASED | CANCEL_HOLD |
| Средства зарезервированы</br>FUNDS_HELD | Резервирование средств отменено</br>FUNDS_RELEASED | RELEASE_FUNDS |
| Средства зарезервированы</br>FUNDS_HELD | Легальность операции проверена</br>ANTIFRAUD_CHECKED | ANTIFRAUD_AUTOCHECK |
| Средства зарезервированы</br>FUNDS_HELD | Легальность операции проверена (операция подозрительна)</br>FRAUD_OPERATION_DETECTED | ANTIFRAUD_AUTOCHECK |
| Средства зарезервированы</br>FUNDS_HELD | Ожидание решения оператора</br>AWAITING_MANUAL_CHECK | ANTIFRAUD_MANUAL_CHECK |
| Ожидание решения оператора</br>AWAITING_MANUAL_CHECK | Легальность операции проверена</br>ANTIFRAUD_CHECKED | ANTIFRAUD_MANUAL_CHECK |
| Ожидание решения оператора</br>AWAITING_MANUAL_CHECK | Легальность операции проверена (операция подозрительна)</br>FRAUD_OPERATION_DETECTED | ANTIFRAUD_MANUAL_CHECK |
| Ожидание решения оператора</br>AWAITING_MANUAL_CHECK | Легальность операции проверена</br>ANTIFRAUD_CHECKED | ANTIFRAUD_TIMEOUT |
| Легальность операции проверена</br>ANTIFRAUD_CHECKED | Средства переведены</br>FUNDS_TRANSFERRED | TRANSFER_FUNDS |
| Легальность операции проверена (операция определена как подозрительная)</br>FRAUD_OPERATION_DETECTED | Служба безопасности уведомлена</br>SECURITY_NOTIFIED | SEND_NOTIFICATION_TO_SECURITY |
| Легальность операции проверена (операция определена как подозрительная)</br>FRAUD_OPERATION_DETECTED | Резервирование средств отменено</br>FUNDS_RELEASED | RELEASE_FUNDS |
| Средства переведены</br>FUNDS_TRANSFERRED | Процесс платежа завершён</br>PAYMENT_PROCESS_COMPLETED | COMPLETE_PAYMENT |
| Средства переведены</br>FUNDS_TRANSFERRED | Процесс оплаты отменён плательщиком</br>PAYMENT_PROCESS_CANCELED | CANCEL_PAYMENT |
| Процесс оплаты отменён плательщиком</br>PAYMENT_PROCESS_CANCELED | Средства возвращены</br>FUNDS_RETURNED | RETURN_FUNDS |
| Резервирование средств отменено</br>FUNDS_RELEASED | Процесс оплаты отменён</br>PAYMENT_PROCESS_CANCELED | FINISH_PAYMENT_PROCESS |
| Средства возвращены</br>FUNDS_RETURNED | Процесс оплаты отменён</br>PAYMENT_PROCESS_CANCELED | FINISH_PAYMENT_PROCESS |
| Средства возвращены</br>FUNDS_RETURNED | Плательщик уведомлён</br>PAYER_NOTIFIED | SEND_NOTIFICATION_TO_USER |
| Средства переведены</br>FUNDS_TRANSFERRED | Плательщик уведомлён</br>PAYER_NOTIFIED | SEND_NOTIFICATION_TO_USER |
| Процесс оплаты отменён</br>PAYMENT_PROCESS_CANCELED | Плательщик уведомлён</br>PAYER_NOTIFIED | SEND_NOTIFICATION_TO_USER |
| Процесс платежа завершён</br>PAYMENT_PROCESS_COMPLETED | Плательщик уведомлён</br>PAYER_NOTIFIED | SEND_NOTIFICATION_TO_USER |
