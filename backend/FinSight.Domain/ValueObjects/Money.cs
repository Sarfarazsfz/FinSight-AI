namespace FinSight.Domain.ValueObjects;

public readonly record struct Money(decimal Amount)
{
    public static Money From(decimal amount)
    {
        return new Money(decimal.Round(amount, 2));
    }

    public Money Add(Money other)
    {
        return From(Amount + other.Amount);
    }

    public Money Subtract(Money other)
    {
        return From(Amount - other.Amount);
    }
}