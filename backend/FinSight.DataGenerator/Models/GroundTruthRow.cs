namespace FinSight.DataGenerator.Models;

public sealed record GroundTruthRow(
    string TransactionReference,
    string ScenarioType,
    string ExpectedStatus,
    string ExpectedReasonCode,
    string ExpectedExceptionCategory,
    bool ExpectedPaymentPresent,
    bool ExpectedBankPresent,
    bool ExpectedSettlementPresent,
    string ExpectedAmountRelationship,
    string ExpectedDateRelationship);