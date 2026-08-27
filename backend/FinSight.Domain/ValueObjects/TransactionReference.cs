namespace FinSight.Domain.ValueObjects;

public readonly record struct TransactionReference
{
    public string Value { get; }

    public TransactionReference(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Transaction reference is required.",
                nameof(value));
        }

        Value = value.Trim();
    }

    public override string ToString() => Value;
}