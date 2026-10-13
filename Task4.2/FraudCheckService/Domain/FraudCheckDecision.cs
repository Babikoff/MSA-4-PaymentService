namespace FraudCheckService.Domain;

/// <summary>Результат антифрод проверки.</summary>
public enum FraudCheckDecision
{
    ALLOW,
    BLOCK,
    MANUAL
}