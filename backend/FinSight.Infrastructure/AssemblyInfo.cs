using System.Runtime.CompilerServices;

// Exposes internal-only members (currently: DependencyInjection's
// AI-configuration-resolution helpers, which translate legacy flat
// AI:DefaultProvider/AI:FallbackEnabled keys into the new nested
// AiProviderOptions shape) to direct unit testing, without requiring a
// full DI container build or a real database connection string.
[assembly: InternalsVisibleTo("FinSight.Tests")]
