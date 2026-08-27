namespace FinSight.Domain.Enums;

public enum AuditEventType
{
    BatchCreated,
    BatchValidated,
    ReconciliationStarted,
    ReconciliationCompleted,
    ReconciliationFailed,
    ReconciliationDecisionRecorded,
    ExceptionCreated,
    AiQuestionAsked,
    AiToolInvoked,
    AiExplanationRequested,
    AiExplanationFailed,
    AiAssistantFailed
}