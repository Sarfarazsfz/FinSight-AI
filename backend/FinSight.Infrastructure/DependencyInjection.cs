using FinSight.Application.Abstractions.Evaluation;
using FinSight.Application.Abstractions.Persistence;
using FinSight.Application.Abstractions.Reconciliation;
using FinSight.Application.Abstractions.Services;
using FinSight.Application.AI;
using FinSight.Application.Evaluation;
using FinSight.Application.Reconciliation;
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

        // Live ground-truth verification: compares caller-supplied
        // ground truth against a run's already-persisted results/
        // exceptions using the shared GroundTruthComparer (also used by
        // the offline FinSight.DataGenerator console verifier).
        services.AddScoped<
            IGroundTruthComparisonService,
            GroundTruthComparisonService>();

        // -----------------------------------------------------------------
        // AI configuration
        // -----------------------------------------------------------------

        // Global provider credentials/model/base-URL -- read exactly
        // once, shared by both F9 (ExceptionExplanation) and F10
        // (FinanceAssistant) below. Neither AI:Gemini:ApiKey nor
        // AI:OpenAI:ApiKey nor AI:Nvidia:ApiKey is read anywhere else in
        // this file.
        var providers =
            new AiProviderOptions.ProvidersOptions
            {
                Gemini =
                    new AiProviderOptions.GeminiOptions
                    {
                        Enabled =
                            bool.TryParse(
                                configuration["AI:Providers:Gemini:Enabled"],
                                out var geminiEnabled)
                                ? geminiEnabled
                                : true,

                        ApiKey =
                            configuration["AI:Providers:Gemini:ApiKey"]
                            ?? string.Empty,

                        Model =
                            configuration["AI:Providers:Gemini:Model"]
                            ?? "gemini-2.5-flash"
                    },

                OpenAI =
                    new AiProviderOptions.OpenAiOptions
                    {
                        Enabled =
                            bool.TryParse(
                                configuration["AI:Providers:OpenAI:Enabled"],
                                out var openAiEnabled)
                                ? openAiEnabled
                                : true,

                        ApiKey =
                            configuration["AI:Providers:OpenAI:ApiKey"]
                            ?? string.Empty,

                        Model =
                            configuration["AI:Providers:OpenAI:Model"]
                            ?? "gpt-5-mini",

                        BaseUrl =
                            configuration["AI:Providers:OpenAI:BaseUrl"]
                    },

                Nvidia =
                    new AiProviderOptions.NvidiaOptions
                    {
                        Enabled =
                            bool.TryParse(
                                configuration["AI:Providers:Nvidia:Enabled"],
                                out var nvidiaEnabled)
                                ? nvidiaEnabled
                                : true,

                        ApiKey =
                            configuration["AI:Providers:Nvidia:ApiKey"]
                            ?? string.Empty,

                        Model =
                            configuration["AI:Providers:Nvidia:Model"]
                            ?? "openai/gpt-oss-120b",

                        BaseUrl =
                            configuration["AI:Providers:Nvidia:BaseUrl"]
                            ?? "https://integrate.api.nvidia.com/v1"
                    }
            };

        var aiOptions =
            new AiProviderOptions
            {
                Providers = providers,

                // New nested key when present; otherwise fully translates
                // the legacy flat AI:DefaultProvider + AI:FallbackEnabled
                // pair into an equivalent order, preserving F9's exact
                // pre-refactor behavior for every existing deployment.
                ExceptionExplanation =
                    ResolveExceptionExplanationOptions(configuration),

                // F10's own nested key, unchanged in spirit from the
                // prior phase -- now also carries its own FallbackEnabled,
                // falling back to the same legacy flat key F10 previously
                // shared with F9 when its own key is absent.
                FinanceAssistant =
                    ResolveFinanceAssistantOptions(configuration)
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
                    options.Providers.Gemini.ApiKey,
                    options.Providers.Gemini.Model);
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
                    options.Providers.OpenAI.ApiKey,
                    options.Providers.OpenAI.Model);
            });

        services.AddScoped<IOpenAiProvider>(
            sp =>
                sp.GetRequiredService<
                    OpenAiProvider>());

        // F9 NVIDIA adapter -- additive, mirrors the
        // IGeminiAiProvider/IOpenAiProvider concrete-registration
        // pattern above. Unlike Gemini/OpenAI, its constructor never
        // throws for missing configuration (see NvidiaAiProvider's own
        // doc comment) -- an unconfigured NVIDIA is excluded from F9's
        // chain via IsAvailable, never a startup-time crash.
        services.AddScoped<NvidiaAiProvider>(
            sp =>
            {
                var options =
                    sp.GetRequiredService<
                        AiProviderOptions>();

                return new NvidiaAiProvider(
                    options.Providers.Nvidia.ApiKey,
                    options.Providers.Nvidia.Model,
                    options.Providers.Nvidia.BaseUrl);
            });

        services.AddScoped<INvidiaAiProvider>(
            sp =>
                sp.GetRequiredService<
                    NvidiaAiProvider>());

        // Keyed registrations, one per canonical provider name -- this is
        // the seam that lets AiProviderRouter resolve a provider by name,
        // on demand, without ever needing all three provider types as
        // unconditional constructor dependencies. A keyed service is
        // constructed only when GetRequiredKeyedService(name) is actually
        // called for that key, so a provider excluded from
        // ExceptionExplanation.ProviderOrder (or disabled) is never
        // touched -- see AiProviderRouter's own doc comment for why this
        // matters (Global AI Provider DI Resolution fix).
        services.AddKeyedScoped<IAiProvider>(
            "Gemini",
            (sp, _) => sp.GetRequiredService<GeminiAiProvider>());

        services.AddKeyedScoped<IAiProvider>(
            "NVIDIA",
            (sp, _) => sp.GetRequiredService<NvidiaAiProvider>());

        services.AddKeyedScoped<IAiProvider>(
            "OpenAI",
            (sp, _) => sp.GetRequiredService<OpenAiProvider>());

        // Single provider exposed to the application layer.
        // Router walks AiProviderOptions.ExceptionExplanation.ProviderOrder,
        // resolving only the providers named in that order via the keyed
        // registrations above (IServiceProvider and AiProviderOptions are
        // both already resolvable, so no explicit factory is needed).
        services.AddScoped<
            IAiProvider,
            AiProviderRouter>();

        // AI explanation application service.
        services.AddScoped<
            IAiExplanationService,
            AiExplanationService>();

        // Finance Assistant Gemini model client. Deliberately does NOT
        // throw when unconfigured (see GeminiFinanceAssistantModelClient's
        // own doc comment) -- the "not configured" failure now surfaces
        // only when the client is actually asked to generate content, not
        // merely because it was resolved.
        services.AddScoped<
            IFinanceAssistantModelClient>(
            sp =>
            {
                var options =
                    sp.GetRequiredService<
                        AiProviderOptions>();

                return new GeminiFinanceAssistantModelClient(
                    options.Providers.Gemini.ApiKey);
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
                    options.Providers.Gemini.Model);
            });

        // Finance Assistant OpenAI provider (concrete registration).
        services.AddScoped<OpenAiFinanceAssistantProvider>(
            sp =>
            {
                var options =
                    sp.GetRequiredService<
                        AiProviderOptions>();

                return new OpenAiFinanceAssistantProvider(
                    options.Providers.OpenAI.ApiKey,
                    options.Providers.OpenAI.Model);
            });

        // Finance Assistant NVIDIA provider (concrete registration,
        // additive -- Gemini/OpenAI registrations above are unchanged).
        services.AddScoped<NvidiaFinanceAssistantProvider>(
            sp =>
            {
                var options =
                    sp.GetRequiredService<
                        AiProviderOptions>();

                return new NvidiaFinanceAssistantProvider(
                    options.Providers.Nvidia.ApiKey,
                    options.Providers.Nvidia.Model,
                    options.Providers.Nvidia.BaseUrl);
            });

        // Keyed registrations, one per canonical provider name -- mirrors
        // the IAiProvider keyed registrations above (Global AI Provider DI
        // Resolution fix). A provider excluded from FinanceAssistant.
        // ProviderOrder (or disabled) is never resolved, so e.g. an
        // unconfigured Gemini's IFinanceAssistantModelClient is never
        // touched by a NVIDIA-only or OpenAI-only configuration.
        services.AddKeyedScoped<IFinanceAssistantProvider>(
            "Gemini",
            (sp, _) => sp.GetRequiredService<GeminiFinanceAssistantProvider>());

        services.AddKeyedScoped<IFinanceAssistantProvider>(
            "NVIDIA",
            (sp, _) => sp.GetRequiredService<NvidiaFinanceAssistantProvider>());

        services.AddKeyedScoped<IFinanceAssistantProvider>(
            "OpenAI",
            (sp, _) => sp.GetRequiredService<OpenAiFinanceAssistantProvider>());

        // Finance Assistant provider: FinanceAssistantProviderRouter
        // walks AiProviderOptions.FinanceAssistant.ProviderOrder (default
        // ["Gemini","OpenAI"], NVIDIA joins only when configured into
        // that order), resolving only the providers named in that order
        // via the keyed registrations above.
        services.AddScoped<
            IFinanceAssistantProvider,
            FinanceAssistantProviderRouter>();

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

    /// <summary>
    /// Resolves the Finance Assistant's provider chain order. Accepts
    /// either a real config array (AI:FinanceAssistant:ProviderOrder:0,
    /// :1, :2 -- e.g. a JSON array in appsettings.json) or a single
    /// comma-separated value (AI:FinanceAssistant:ProviderOrder=
    /// Gemini,NVIDIA,OpenAI -- simpler to set as one environment
    /// variable). Falls back to the pre-NVIDIA default when neither form
    /// is configured, so every existing deployment is unaffected.
    /// </summary>
    /// <summary>
    /// F9's chain configuration. Uses the new AI:ExceptionExplanation:*
    /// keys when present; otherwise fully translates the legacy flat
    /// AI:DefaultProvider + AI:FallbackEnabled pair into an equivalent
    /// order, reproducing AiProviderRouter's exact pre-refactor behavior
    /// (including "an unrecognized DefaultProvider value resolves to an
    /// empty order", which the router turns into
    /// AiProviderUnavailableException, matching UnsupportedProvider_Throws).
    /// </summary>
    internal static AiProviderOptions.SurfaceOptions ResolveExceptionExplanationOptions(
        IConfiguration configuration)
    {
        var configuredOrder =
            ReadProviderOrderOrNull(
                configuration,
                "AI:ExceptionExplanation:ProviderOrder");

        var legacyFallbackEnabled =
            bool.TryParse(
                configuration["AI:FallbackEnabled"],
                out var parsedLegacyFallback)
                ? parsedLegacyFallback
                : true;

        var order =
            configuredOrder
            ?? TranslateLegacyDefaultProviderToOrder(
                configuration["AI:DefaultProvider"] ?? "Gemini",
                legacyFallbackEnabled);

        var fallbackEnabled =
            bool.TryParse(
                configuration["AI:ExceptionExplanation:FallbackEnabled"],
                out var parsedFallback)
                ? parsedFallback
                : legacyFallbackEnabled;

        return new AiProviderOptions.SurfaceOptions
        {
            ProviderOrder = order,
            FallbackEnabled = fallbackEnabled
        };
    }

    /// <summary>
    /// F10's chain configuration. ProviderOrder already had its own
    /// dedicated AI:FinanceAssistant:ProviderOrder key from the prior
    /// NVIDIA phase (no legacy translation needed for the order itself --
    /// F10 never had a DefaultProvider-style key). FallbackEnabled is new
    /// here: F10 previously read the same shared flat AI:FallbackEnabled
    /// F9 used, so that remains the fallback default when F10's own key
    /// is absent, preserving any existing deployment's configured value.
    /// </summary>
    internal static AiProviderOptions.SurfaceOptions ResolveFinanceAssistantOptions(
        IConfiguration configuration)
    {
        var order =
            ReadProviderOrderOrNull(
                configuration,
                "AI:FinanceAssistant:ProviderOrder")
            ?? new[] { "Gemini", "OpenAI" };

        var legacyFallbackEnabled =
            bool.TryParse(
                configuration["AI:FallbackEnabled"],
                out var parsedLegacyFallback)
                ? parsedLegacyFallback
                : true;

        var fallbackEnabled =
            bool.TryParse(
                configuration["AI:FinanceAssistant:FallbackEnabled"],
                out var parsedFallback)
                ? parsedFallback
                : legacyFallbackEnabled;

        return new AiProviderOptions.SurfaceOptions
        {
            ProviderOrder = order,
            FallbackEnabled = fallbackEnabled
        };
    }

    /// <summary>
    /// Reads a provider order from either a real config array (e.g.
    /// Key:0, Key:1, Key:2 -- a JSON array in appsettings.json) or a
    /// single comma-separated value (simpler to set as one environment
    /// variable). Returns null -- not an empty array -- when neither form
    /// is configured, so callers can distinguish "nothing configured"
    /// from "explicitly configured empty".
    /// </summary>
    internal static IReadOnlyList<string>? ReadProviderOrderOrNull(
        IConfiguration configuration,
        string key)
    {
        var fromArray =
            configuration.GetSection(key).Get<string[]>();

        if (fromArray is { Length: > 0 })
        {
            return fromArray;
        }

        var flatValue = configuration[key];

        if (!string.IsNullOrWhiteSpace(flatValue))
        {
            return flatValue
                .Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries);
        }

        return null;
    }

    /// <summary>
    /// Reproduces AiProviderRouter's pre-refactor primary/fallback
    /// resolution as an equivalent ordered name list. An unrecognized
    /// DefaultProvider value (anything other than "gemini"/"openai",
    /// case-insensitive) resolves to an empty order -- exactly today's
    /// "no configured AI provider is available" behavior.
    /// </summary>
    internal static IReadOnlyList<string> TranslateLegacyDefaultProviderToOrder(
        string defaultProvider,
        bool fallbackEnabled)
    {
        var primaryName =
            defaultProvider.Trim().ToLowerInvariant() switch
            {
                "gemini" => "Gemini",
                "openai" => "OpenAI",
                _ => null
            };

        if (primaryName is null)
        {
            return Array.Empty<string>();
        }

        if (!fallbackEnabled)
        {
            return new[] { primaryName };
        }

        var otherName = primaryName == "Gemini" ? "OpenAI" : "Gemini";

        return new[] { primaryName, otherName };
    }
}
