using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Reconciliation;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.AI;
using FinSight.Application.Reconciliation;
using Google.GenAI;
using FinSight.Infrastructure.AI;
using FinSight.Infrastructure.AI.Gemini;
using FinSight.Infrastructure.AI.OpenAI;
using FinSight.Infrastructure.Authentication;
using FinSight.Infrastructure.FileParsing;
using FinSight.Infrastructure.Ingestion;
using FinSight.Infrastructure.Persistence;
using FinSight.Infrastructure.Reconciliation;
using FinSight.Infrastructure.Reconciliation.Strategies;
using FinSight.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Get PostgreSQL connection string from configuration/user secrets.
        var connectionString =
            configuration.GetConnectionString("FinSightDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'FinSightDb' was not found.");
        }

        // Register EF Core DbContext with PostgreSQL.
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        // Register CSV parser.
        services.AddScoped<
            ISourceCsvParser,
            SourceCsvParser>();

        // Register batch ingestion validator.
        services.AddScoped<
            IBatchIngestionValidator,
            BatchIngestionValidator>();

        // Register batch ingestion service.
        services.AddScoped<
            IBatchIngestionService,
            BatchIngestionService>();

        // Register deterministic reconciliation strategies.
        services.AddScoped<
            IExactReferenceMatchStrategy,
            StrategyOneExactReferenceMatch>();

        services.AddScoped<
            IAmountDateToleranceMatchStrategy,
            StrategyTwoAmountDateToleranceMatch>();

        // Register deterministic classifier.
        services.AddScoped<MatchClassifier>();

        // Register reconciliation application service.
        services.AddScoped<
            IReconciliationService,
            ReconciliationOrchestrator>();

        // Single authoritative summary calculation, shared by
        // ReconciliationController.GetSummary and the Finance
        // Assistant's getReconciliationSummary tool.
        services.AddScoped<
            IReconciliationSummaryBuilder,
            ReconciliationSummaryBuilder>();

        // -----------------------------------------------------------------
        // AI configuration
        // -----------------------------------------------------------------

        var aiOptions =
            new AiProviderOptions
            {
                Gemini =
                    new AiProviderOptions.GeminiOptions
                    {
                        ApiKey =
                            configuration["AI:Gemini:ApiKey"]
                            ?? string.Empty,

                        Model =
                            configuration["AI:Gemini:Model"]
                            ?? "gemini-2.5-flash"
                    },

                OpenAI =
                    new AiProviderOptions.OpenAiOptions
                    {
                        ApiKey =
                            configuration["AI:OpenAI:ApiKey"]
                            ?? string.Empty,

                        Model =
                            configuration["AI:OpenAI:Model"]
                            ?? "gpt-5-mini"
                    },

                DefaultProvider =
                    configuration["AI:DefaultProvider"]
                    ?? "Gemini",

                FallbackEnabled =
                    bool.TryParse(
                        configuration["AI:FallbackEnabled"],
                        out var fallbackEnabled)
                        ? fallbackEnabled
                        : true
            };

        services.AddSingleton(aiOptions);

        // -----------------------------------------------------------------
        // AI providers
        // -----------------------------------------------------------------

        services.AddScoped<GeminiAiProvider>(
            sp =>
            {
                var options =
                    sp.GetRequiredService<
                        AiProviderOptions>();

                return new GeminiAiProvider(
                    options.Gemini.ApiKey,
                    options.Gemini.Model);
            });

        services.AddScoped<IGeminiAiProvider>(
            sp =>
                sp.GetRequiredService<
                    GeminiAiProvider>());

        services.AddScoped<OpenAiProvider>(
            sp =>
            {
                var options =
                    sp.GetRequiredService<
                        AiProviderOptions>();

                return new OpenAiProvider(
                    options.OpenAI.ApiKey,
                    options.OpenAI.Model);
            });

        services.AddScoped<IOpenAiProvider>(
            sp =>
                sp.GetRequiredService<
                    OpenAiProvider>());

        // Single provider exposed to the application layer.
        // Router decides primary provider and fallback provider.
        services.AddScoped<
            IAiProvider,
            AiProviderRouter>();

        // AI explanation application service.
        services.AddScoped<
            IAiExplanationService,
            AiExplanationService>();

        // Finance Assistant Gemini model client.
        services.AddScoped<
            IFinanceAssistantModelClient>(
            sp =>
            {
                var options =
                    sp.GetRequiredService<
                        AiProviderOptions>();

                if (string.IsNullOrWhiteSpace(
                        options.Gemini.ApiKey))
                {
                    throw new InvalidOperationException(
                        "Gemini API key is required for the Finance Assistant.");
                }

                var client =
                    new Client(
                        apiKey:
                            options.Gemini.ApiKey);

                return new GeminiFinanceAssistantModelClient(
                    client);
            });

        // Finance Assistant Gemini provider (concrete registration so the
        // router below can depend on both Gemini and OpenAI without
        // ambiguity, mirroring the IGeminiAiProvider/IOpenAiProvider
        // pattern already used for AiProviderRouter above).
        services.AddScoped<GeminiFinanceAssistantProvider>(
            sp =>
            {
                var options =
                    sp.GetRequiredService<
                        AiProviderOptions>();

                return new GeminiFinanceAssistantProvider(
                    sp.GetRequiredService<
                        IFinanceAssistantModelClient>(),
                    options.Gemini.Model);
            });

        // Finance Assistant OpenAI provider (concrete registration).
        services.AddScoped<OpenAiFinanceAssistantProvider>(
            sp =>
            {
                var options =
                    sp.GetRequiredService<
                        AiProviderOptions>();

                return new OpenAiFinanceAssistantProvider(
                    options.OpenAI.ApiKey,
                    options.OpenAI.Model);
            });

        // Finance Assistant provider: FinanceAssistantProviderRouter
        // decides primary vs. fallback (per AI:DefaultProvider), the
        // same router class already proven correct in
        // FinanceAssistantProviderRouterTests -- previously this was
        // bound directly to Gemini with no fallback ever wired in.
        services.AddScoped<
            IFinanceAssistantProvider>(
            sp =>
            {
                var options =
                    sp.GetRequiredService<
                        AiProviderOptions>();

                return new FinanceAssistantProviderRouter(
                    sp.GetRequiredService<
                        GeminiFinanceAssistantProvider>(),
                    sp.GetRequiredService<
                        OpenAiFinanceAssistantProvider>(),
                    options);
            });

        // Finance Assistant orchestration service.
        services.AddScoped<
            IFinanceAssistantService,
            FinanceAssistantService>();

        // Finance tool implementations.
        services.AddScoped<
            IReconciliationSummaryTool,
            ReconciliationSummaryTool>();

        services.AddScoped<
            IUnmatchedRecordsTool,
            UnmatchedRecordsTool>();

        services.AddScoped<
            ITransactionDetailsTool,
            TransactionDetailsTool>();

        services.AddScoped<
            IExceptionDetailsTool,
            ExceptionDetailsTool>();

        // Expose the four allowed tools through IFinanceTool.
        services.AddScoped<IFinanceTool>(
            sp =>
                sp.GetRequiredService<
                    IReconciliationSummaryTool>());

        services.AddScoped<IFinanceTool>(
            sp =>
                sp.GetRequiredService<
                    IUnmatchedRecordsTool>());

        services.AddScoped<IFinanceTool>(
            sp =>
                sp.GetRequiredService<
                    ITransactionDetailsTool>());

        services.AddScoped<IFinanceTool>(
            sp =>
                sp.GetRequiredService<
                    IExceptionDetailsTool>());

        services.AddScoped<
            IFinanceToolRegistry,
            FinanceToolRegistry>();

        // -----------------------------------------------------------------
        // Authentication
        // -----------------------------------------------------------------

        services.AddScoped<
            IPasswordService,
            PasswordService>();

        services.AddScoped<
            IJwtTokenService,
            JwtTokenService>();

        services.AddScoped<
            IAuthService,
            AuthService>();

        // -----------------------------------------------------------------
        // Persistence
        // -----------------------------------------------------------------

        // Register batch repository.
        services.AddScoped<
            IBatchRepository,
            BatchRepository>();

        // Register user repository.
        services.AddScoped<
            IUserRepository,
            UserRepository>();

        // Register raw Payment repository.
        services.AddScoped<
            IPaymentRecordRepository,
            PaymentRecordRepository>();

        // Register raw Bank repository.
        services.AddScoped<
            IBankRecordRepository,
            BankRecordRepository>();

        // Register raw Settlement repository.
        services.AddScoped<
            ISettlementRecordRepository,
            SettlementRecordRepository>();

        // Register normalized transaction repository.
        services.AddScoped<
            INormalizedTransactionRepository,
            NormalizedTransactionRepository>();

        // Register reconciliation run repository.
        services.AddScoped<
            IReconciliationRunRepository,
            ReconciliationRunRepository>();

        // Register reconciliation result repository.
        services.AddScoped<
            IReconciliationResultRepository,
            ReconciliationResultRepository>();

        // Register reconciliation exception repository.
        services.AddScoped<
            IReconciliationExceptionRepository,
            ReconciliationExceptionRepository>();

        // Register audit log writer.
        services.AddScoped<
            IAuditLogWriter,
            AuditLogRepository>();

        // Register Unit of Work.
        services.AddScoped<
            IUnitOfWork,
            UnitOfWork>();

        return services;
    }
}
