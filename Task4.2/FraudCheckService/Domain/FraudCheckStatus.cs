namespace FraudCheckService.Domain;

/// <summary>Статусы процесса антифрод проверки.</summary>
public enum FraudCheckStatus
{
    IN_PROGRESS,
    AWAITING_MANUAL_CHECK,
    ANTIFRAUD_CHECKED,
    FRAUD_OPERATION_DETECTED
}