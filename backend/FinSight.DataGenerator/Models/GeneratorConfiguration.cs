namespace FinSight.DataGenerator.Models;

public sealed class GeneratorConfiguration
{
    public const int Seed = 42026;

    public const int TotalLogicalTransactions = 100;

    public const int MatchedCount = 70;
    public const int ExactMatchCount = 60;
    public const int ToleranceMatchCount = 10;

    public const int MismatchedCount = 10;
    public const int AmountMismatchCount = 8;
    public const int DateMismatchCount = 2;

    public const int MissingCount = 12;
    public const int MissingBankCount = 5;
    public const int MissingSettlementCount = 4;
    public const int MissingPaymentCount = 3;

    public const int DuplicateCount = 6;
    public const int DuplicatePaymentCount = 3;
    public const int DuplicateBankCount = 2;
    public const int DuplicateSettlementCount = 1;

    public const int UnresolvedCount = 2;

    public const decimal DefaultAmount = 1000.00m;

    public static int ExpectedPaymentRows =>
        TotalLogicalTransactions
        - MissingPaymentCount
        + DuplicatePaymentCount;

    public static int ExpectedBankRows =>
        TotalLogicalTransactions
        - MissingBankCount
        + DuplicateBankCount;

    public static int ExpectedSettlementRows =>
        TotalLogicalTransactions
        - MissingSettlementCount
        + DuplicateSettlementCount;

    public static int ExpectedRawRowCount =>
        ExpectedPaymentRows
        + ExpectedBankRows
        + ExpectedSettlementRows;

    public static decimal ExpectedMatchRate =>
        70.00m;
}