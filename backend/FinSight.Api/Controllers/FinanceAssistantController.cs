using FinSight.Api.Authentication;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Controllers;

[ApiController]
[Route("api/finance-assistant")]
[Authorize]
public sealed class FinanceAssistantController : ControllerBase
{
    private readonly IFinanceAssistantService _assistantService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IBatchAccessService _batchAccessService;

    public FinanceAssistantController(
        IFinanceAssistantService assistantService,
        ICurrentUserService currentUserService,
        IBatchAccessService batchAccessService)
    {
        _assistantService = assistantService;
        _currentUserService = currentUserService;
        _batchAccessService = batchAccessService;
    }

    [HttpPost("ask")]
    [ProducesResponseType(
        typeof(FinanceAssistantResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FinanceAssistantResponse>> Ask(
        [FromBody] FinanceAssistantRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Problem(
                detail: "Request body is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        if (request.RunId == Guid.Empty)
        {
            return Problem(
                detail: "runId is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return Problem(
                detail: "question is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        if (!_currentUserService.TryGetCurrentUserId(out var currentUserId))
        {
            return Problem(
                detail: "Authentication is required.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized");
        }

        // The assistant reads real reconciliation data through its
        // tools -- it must not be reachable for a run the caller does
        // not own. Checked before AskAsync is called at all.
        var ownedRun =
            await _batchAccessService.GetOwnedRunAsync(
                request.RunId,
                currentUserId,
                cancellationToken);

        if (ownedRun is null)
        {
            return Problem(
                detail:
                    $"Reconciliation run '{request.RunId}' was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource Not Found");
        }

        try
        {
            var response =
                await _assistantService.AskAsync(
                    request,
                    cancellationToken);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }
    }
}
