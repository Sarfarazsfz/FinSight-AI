namespace FinSight.Application.AI;

public interface IFinanceAssistantService
{
    Task<FinanceAssistantResponse> AskAsync(
        FinanceAssistantRequest request,
        CancellationToken cancellationToken = default);
}
