namespace FinSight.Application.AI;

public interface IFinanceAssistantProvider
{
    string ProviderName { get; }

    Task<FinanceAssistantProviderResponse> AskAsync(
        FinanceAssistantProviderRequest request,
        CancellationToken cancellationToken = default);
}
