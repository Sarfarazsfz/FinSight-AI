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

    public FinanceAssistantController(
        IFinanceAssistantService assistantService)
    {
        _assistantService = assistantService;
    }

    [HttpPost("ask")]
    [ProducesResponseType(
        typeof(FinanceAssistantResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status401Unauthorized)]
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
