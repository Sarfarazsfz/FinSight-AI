using FinSight.Application.Abstractions.Services;
using FinSight.Application.AI;
using FinSight.Api.Controllers;
using FinSight.Domain.Entities;
using FinSight.Tests.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Tests.Api;

[TestFixture]
public sealed class FinanceAssistantControllerTests
{
    private static readonly Guid ValidRunId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid CurrentUserId = Guid.NewGuid();

    /// <summary>
    /// Owns exactly ValidRunId for CurrentUserId, and nothing else -- lets
    /// the not-owned test below use any other Guid to prove rejection
    /// without needing a real database.
    /// </summary>
    private static FinanceAssistantController CreateController(
        FakeFinanceAssistantService service) =>
        new(
            service,
            new FixedCurrentUserService(CurrentUserId),
            new FakeBatchAccessService(ValidRunId, CurrentUserId));

    private sealed class FakeBatchAccessService : IBatchAccessService
    {
        private readonly Guid _ownedRunId;
        private readonly Guid _ownerUserId;

        public FakeBatchAccessService(Guid ownedRunId, Guid ownerUserId)
        {
            _ownedRunId = ownedRunId;
            _ownerUserId = ownerUserId;
        }

        public Task<Batch?> GetOwnedBatchAsync(
            Guid batchId,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "Ask() checks run ownership, not batch ownership directly.");

        public Task<ReconciliationRun?> GetOwnedRunAsync(
            Guid runId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var owned =
                runId == _ownedRunId && userId == _ownerUserId
                    ? new ReconciliationRun(Guid.NewGuid())
                    : null;

            return Task.FromResult(owned);
        }
    }

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
            CreateController(service);

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
            CreateController(service);

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
            CreateController(service);

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
            CreateController(service);

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
    public async Task Ask_WithARunIdNotOwnedByTheCurrentUser_Returns404WithoutCallingTheAssistant()
    {
        var service =
            new FakeFinanceAssistantService(
                new FinanceAssistantResponse());

        var controller =
            CreateController(service);

        var someoneElsesRunId = Guid.NewGuid();

        var result =
            await controller.Ask(
                new FinanceAssistantRequest
                {
                    RunId = someoneElsesRunId,
                    Question = "Summarize this run."
                },
                CancellationToken.None);

        var objectResult =
            result.Result as ObjectResult;

        Assert.That(objectResult, Is.Not.Null);

        Assert.That(
            objectResult!.StatusCode,
            Is.EqualTo(StatusCodes.Status404NotFound));

        // The assistant -- and therefore its AI provider and its
        // read-only tools -- must never be invoked for a run the caller
        // does not own.
        Assert.That(service.Calls, Is.EqualTo(0));
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
