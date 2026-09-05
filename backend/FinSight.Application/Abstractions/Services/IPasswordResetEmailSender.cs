namespace FinSight.Application.Abstractions.Services;

/// <summary>
/// Delivery of one password-reset link.
///
/// Deliberately narrow: this is not a general-purpose email abstraction,
/// because password reset is the only thing in FinSight that sends mail.
/// A broader IEmailSender would be speculative surface area.
///
/// The reset URL passed here contains the single raw token that exists in
/// the system. An implementation must treat it as a credential: never log
/// it, never persist it, never forward it anywhere but the recipient
/// address.
/// </summary>
public interface IPasswordResetEmailSender
{
    Task SendAsync(
        string recipientEmail,
        string resetUrl,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default);
}
