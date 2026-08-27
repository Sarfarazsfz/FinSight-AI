using System.Text.Json;
using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.DTOs.Ai;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;

namespace FinSight.Infrastructure.AI;

public sealed class AiExplanationService
    : IAiExplanationService
{
    private readonly IReconciliationExceptionRepository
        _exceptionRepository;

    private readonly IReconciliationResultRepository
        _resultRepository;

    private readonly INormalizedTransactionRepository
        _normalizedTransactionRepository;

    private readonly IAuditLogWriter
        _auditLogWriter;

    private readonly IUnitOfWork
        _unitOfWork;

    private readonly IAiProvider
        _aiProvider;

    public AiExplanationService(
        IReconciliationExceptionRepository exceptionRepository,
        IReconciliationResultRepository resultRepository,
        INormalizedTransactionRepository normalizedTransactionRepository,
        IAuditLogWriter auditLogWriter,
        IUnitOfWork unitOfWork,
        IAiProvider aiProvider)
    {
        _exceptionRepository =
            exceptionRepository;

        _resultRepository =
            resultRepository;

        _normalizedTransactionRepository =
            normalizedTransactionRepository;

        _auditLogWriter =
            auditLogWriter;

        _unitOfWork =
            unitOfWork;

        _aiProvider =
            aiProvider;
    }

    public async Task<AiExplanationResponse> ExplainAsync(
        Guid exceptionId,
        CancellationToken cancellationToken = default)
    {
        if (exceptionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Exception ID is required.",
                nameof(exceptionId));
        }

        var exception =
            await _exceptionRepository.GetByIdAsync(
                exceptionId,
                cancellationToken);

        if (exception is null)
        {
            throw new KeyNotFoundException(
                $"Reconciliation exception '{exceptionId}' was not found.");
        }

        var result =
            await _resultRepository.GetByIdAsync(
                exception.ReconciliationResultId,
                cancellationToken);

        if (result is null ||
            result.RunId != exception.RunId)
        {
            throw new InvalidOperationException(
                $"Reconciliation result " +
                $"'{exception.ReconciliationResultId}' " +
                "is missing or does not belong to the exception run.");
        }

        var normalizedTransaction =
            await _normalizedTransactionRepository.GetByIdAsync(
                result.NormalizedTransactionId,
                cancellationToken);

        if (normalizedTransaction is null ||
            normalizedTransaction.RunId != exception.RunId)
        {
            throw new InvalidOperationException(
                $"Normalized transaction " +
                $"'{result.NormalizedTransactionId}' " +
                "is missing or does not belong to the exception run.");
        }

        var request =
            new AiExplanationRequest
            {
                ExceptionId =
                    exception.Id,

                RunId =
                    exception.RunId,

                ReconciliationResultId =
                    exception.ReconciliationResultId,

                TransactionReference =
                    normalizedTransaction.TransactionReference,

                DeterministicCategory =
                    exception.Category.ToString(),

                InvolvedSources =
                    exception.InvolvedSources,

                DiscrepancyDetail =
                    exception.DiscrepancyDetail
            };

        AiExplanationResponse aiResponse;

        try
        {
            aiResponse =
                await _aiProvider.GenerateExplanationAsync(
                    request,
                    cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var failedPayload =
                JsonSerializer.Serialize(
                    new
                    {
                        exception_id =
                            exception.Id,

                        run_id =
                            exception.RunId,

                        provider =
                            _aiProvider.ProviderName,

                        error_type =
                            ex.GetType().Name,

                        error_message =
                            ex.Message
                    });

            await _auditLogWriter.AddAsync(
                new AuditLog(
                    AuditEventType.AiExplanationFailed,
                    failedPayload,
                    exception.RunId,
                    relatedEntityType:
                        "ReconciliationException",
                    relatedEntityId:
                        exception.Id),
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            throw;
        }

        if (string.IsNullOrWhiteSpace(
                aiResponse.Explanation))
        {
            throw new InvalidOperationException(
                "AI provider returned an empty explanation.");
        }

        // The actual provider is known only after the router
        // completes the request. This makes the successful audit
        // record authoritative even when fallback is used.
        var requestedPayload =
    JsonSerializer.Serialize(
        new
        {
            exception_id =
                exception.Id,

            run_id =
                exception.RunId,

            reconciliation_result_id =
                exception.ReconciliationResultId,

            transaction_reference =
                normalizedTransaction.TransactionReference,

            category =
                exception.Category.ToString(),

            requested_provider =
                _aiProvider.ProviderName
        });

        await _auditLogWriter.AddAsync(
            new AuditLog(
                AuditEventType.AiExplanationRequested,
                requestedPayload,
                exception.RunId,
                relatedEntityType:
                    "ReconciliationException",
                relatedEntityId:
                    exception.Id),
            cancellationToken);

        exception.AddAiExplanation(
            aiResponse.Explanation,
            aiResponse.SuggestedCategory);

        var toolPayload =
            JsonSerializer.Serialize(
                new
                {
                    exception_id =
                        exception.Id,

                    run_id =
                        exception.RunId,

                    provider =
                        aiResponse.Provider,

                    generated_at_utc =
                        aiResponse.GeneratedAtUtc
                });

        await _auditLogWriter.AddAsync(
            new AuditLog(
                AuditEventType.AiToolInvoked,
                toolPayload,
                exception.RunId,
                relatedEntityType:
                    "ReconciliationException",
                relatedEntityId:
                    exception.Id),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return aiResponse;
    }
}