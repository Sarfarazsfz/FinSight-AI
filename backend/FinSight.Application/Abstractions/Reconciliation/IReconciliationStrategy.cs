using FinSight.Application.DTOs.Reconciliation;

namespace FinSight.Application.Abstractions.Reconciliation;

public interface IReconciliationStrategy
{
    StrategyEvidence Evaluate(
        ReconciliationEvidence evidence,
        StrategyEvidence? previousEvidence = null);
}