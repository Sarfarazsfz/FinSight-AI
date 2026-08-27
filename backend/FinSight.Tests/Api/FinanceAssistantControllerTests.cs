using FinSight.Application.AI;
using FinSight.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Tests.Api;

[TestFixture]
public sealed class FinanceAssistantControllerTests
{
    private static readonly Guid ValidRunId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Test]
    public async Task Ask_WithValidRequest_ReturnsOk()
    {
        var expected =
            new FinanceAssistantResponse
            {
                Answer = "Reconciliation is complete.",
                ToolsUsed =
                    new[]
                    {
                        "getReconciliationSummary"
                    },
                TraceId = "trace-123"
            };

        var service =
            new FakeFinanceAssistantService(expected);

        var controller =
            new FinanceAssistantController(service);

        var result =
            await controller.Ask(
                new FinanceAssistantRequest
                {
                    RunId = ValidRunId,
                    Question = "Summarize this run."
                },
                CancellationToken.None);

        var okResult =
            result.Result as OkObjectResult;

        Assert.That(
            okResult,
            Is.Not.Null);

        Assert.That(
            okResult!.Value,
            Is.SameAs(expected));

        Assert.That(
            service.Calls,
            Is.EqualTo(1));
    }

    [Test]
    public async Task Ask_WithEmptyRunId_ReturnsBadRequest()
    {
        var service =
            new FakeFinanceAssistantService(
                new FinanceAssistantResponse());

        var controller =
            new FinanceAssistantController(service);

        var result =
            await controller.Ask(
                new FinanceAssistantRequest
                {
                    RunId = Guid.Empty,
                    Question = "Summarize this run."
                },
                CancellationToken.None);

        // Phase 3: standardized on ProblemDetails via Problem(...), which
        // returns a plain ObjectResult (not BadRequestObjectResult) --
        // the HTTP status code is the actual contract, not the CLR
        // result type used to build it.
        var objectResult =
            result.Result as ObjectResult;

        Assert.That(
            objectResult,
            Is.Not.Null);

        Assert.That(
            objectResult!.StatusCode,
            Is.EqualTo(StatusCodes.Status400BadRequest));

        Assert.That(
            service.Calls,
            Is.EqualTo(0));
    }

    [Test]
    public async Task Ask_WithBlankQuestion_ReturnsBadRequest()
    {
        var service =
            new FakeFinanceAssistantService(
                new FinanceAssistantResponse());

        var controller =
            new FinanceAssistantController(service);

        var result =
            await controller.Ask(
                new FinanceAssistantRequest
                {
                    RunId = ValidRunId,
                    Question = "   "
                },
                CancellationToken.None);

        var objectResult =
            result.Result as ObjectResult;

        Assert.That(
            objectResult,
            Is.Not.Null);

        Assert.That(
            objectResult!.StatusCode,
            Is.EqualTo(StatusCodes.Status400BadRequest));

        Assert.That(
            service.Calls,
            Is.EqualTo(0));
    }

    [Test]
    public async Task Ask_WhenServiceThrowsArgumentException_ReturnsBadRequest()
    {
        var service =
            new FakeFinanceAssistantService(
                new ArgumentException(
                    "Question is required."));

        var controller =
            new FinanceAssistantController(service);

        var result =
            await controller.Ask(
                new FinanceAssistantRequest
                {
                    RunId = ValidRunId,
                    Question = "Summarize this run."
                },
                CancellationToken.None);

        var objectResult =
            result.Result as ObjectResult;

        Assert.That(
            objectResult,
            Is.Not.Null);

        Assert.That(
            objectResult!.StatusCode,
            Is.EqualTo(StatusCodes.Status400BadRequest));

        Assert.That(
            service.Calls,
            Is.EqualTo(1));
    }

    [Test]
    public void Controller_HasAuthorizeAttribute()
    {
        var attribute =
            (Microsoft.AspNetCore.Authorization.AuthorizeAttribute?)
                Attribute.GetCustomAttribute(
                    typeof(FinanceAssistantController),
                    typeof(
                        Microsoft.AspNetCore.Authorization.AuthorizeAttribute));

        Assert.That(
            attribute,
            Is.Not.Null);
    }

    private sealed class FakeFinanceAssistantService
        : IFinanceAssistantService
    {
        private readonly FinanceAssistantResponse?
            _response;

        private readonly Exception?
            _exception;

        public FakeFinanceAssistantService(
            FinanceAssistantResponse response)
        {
            _response = response;
        }

        public FakeFinanceAssistantService(
            Exception exception)
        {
            _exception = exception;
        }

        public int Calls { get; private set; }

        public Task<FinanceAssistantResponse> AskAsync(
            FinanceAssistantRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;

            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(
                _response!);
        }
    }
}
