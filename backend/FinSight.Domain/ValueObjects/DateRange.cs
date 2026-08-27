namespace FinSight.Domain.ValueObjects;

public readonly record struct DateRange
{
    public DateOnly StartDate { get; }

    public DateOnly EndDate { get; }

    public DateRange(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException(
                "EndDate must be greater than or equal to StartDate.",
                nameof(endDate));
        }

        StartDate = startDate;
        EndDate = endDate;
    }
}