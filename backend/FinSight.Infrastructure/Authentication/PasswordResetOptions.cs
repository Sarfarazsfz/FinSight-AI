namespace FinSight.Infrastructure.Authentication;

/// <summary>
/// Password-reset configuration, bound from the "Auth:PasswordReset"
/// section. Both values have safe local-development defaults so a fresh
/// clone works without extra configuration; a real deployment must set
/// FrontendBaseUrl to its actual origin, because that value becomes the
/// host of the reset link sent to users.
/// </summary>
public sealed class PasswordResetOptions
{
    /// <summary>
    /// Origin of the Angular app, used to build the reset link. Defaults
    /// to the dev-server origin the API's CORS policy already allows.
    /// </summary>
    public string FrontendBaseUrl { get; init; } =
        "http://localhost:4200";

    /// <summary>
    /// How long a reset link stays redeemable. Short by design: the link
    /// is a bearer credential sitting in an inbox.
    /// </summary>
    public TimeSpan Lifetime { get; init; } =
        TimeSpan.FromMinutes(60);

    /// <summary>
    /// Directory (relative to the API's working directory) that the
    /// Development-only file sink writes reset links into. Git-ignored --
    /// these files contain live reset credentials.
    /// </summary>
    public string DevelopmentSinkDirectory { get; init; } =
        "dev-password-resets";
}
