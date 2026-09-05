using FinSight.Application.Abstractions.Services;
using FinSight.Infrastructure.Authentication;

namespace FinSight.Infrastructure.Email;

/// <summary>
/// DEVELOPMENT-ONLY reset delivery: writes the reset link to a local file
/// sink instead of sending mail.
///
/// FinSight has no email provider configured, and a fake SMTP sender that
/// silently dropped mail would be worse than an explicit sink -- a
/// developer would have no way to complete a reset and no way to tell
/// delivery had failed.
///
/// The link is written to a file rather than the application log on
/// purpose: logs get shipped, aggregated and shared, and this URL is a
/// live credential.
///
/// Deliberately depends on nothing but its options -- no IHostEnvironment,
/// no ILogger. AddInfrastructure is consumed by a plain ServiceCollection
/// in several places (the create-user command, the AI DI tests), and an
/// email sender must not be the reason those callers are forced to build a
/// full host. Whether this implementation is registered at all is decided
/// once, at registration time, in DependencyInjection.
/// </summary>
public sealed class FilePasswordResetEmailSender
    : IPasswordResetEmailSender
{
    private readonly PasswordResetOptions _options;

    public FilePasswordResetEmailSender(PasswordResetOptions options)
    {
        _options = options;
    }

    public async Task SendAsync(
        string recipientEmail,
        string resetUrl,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        var directory =
            Path.Combine(
                Directory.GetCurrentDirectory(),
                _options.DevelopmentSinkDirectory);

        Directory.CreateDirectory(directory);

        var path =
            Path.Combine(
                directory,
                $"reset-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.txt");

        var contents =
            $"""
             FinSight password reset (DEVELOPMENT SINK -- no email was sent)

             To:      {recipientEmail}
             Expires: {expiresAtUtc:u}

             Open this link to set a new password:

             {resetUrl}
             """;

        await File.WriteAllTextAsync(path, contents, cancellationToken);
    }
}

/// <summary>
/// What every non-Development environment gets until a real email provider
/// is wired up.
///
/// It fails when a reset is actually attempted, not when it is
/// constructed: an unconfigured mail provider must not stop the API from
/// starting or block unrelated requests, but it must never look like a
/// reset was delivered when nothing was sent.
/// </summary>
public sealed class UnconfiguredPasswordResetEmailSender
    : IPasswordResetEmailSender
{
    public Task SendAsync(
        string recipientEmail,
        string resetUrl,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "No password reset email provider is configured for this " +
            "environment. Configure one before enabling password reset " +
            "outside Development.");
    }
}
