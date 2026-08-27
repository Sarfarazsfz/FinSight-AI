namespace FinSight.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }

    public string Email { get; private set; } =
        string.Empty;

    public string PasswordHash { get; private set; } =
        string.Empty;

    public string Role { get; private set; } =
        string.Empty;

    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private User()
    {
    }

    public User(
        string email,
        string passwordHash,
        string role)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException(
                "Email is required.",
                nameof(email));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException(
                "Password hash is required.",
                nameof(passwordHash));
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            throw new ArgumentException(
                "Role is required.",
                nameof(role));
        }

        Id = Guid.NewGuid();

        Email = email.Trim().ToLowerInvariant();

        PasswordHash = passwordHash;

        Role = role.Trim();

        IsActive = true;

        CreatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }
}
