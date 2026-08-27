namespace FinSight.Infrastructure.Authentication;

public interface IPasswordService
{
    string HashPassword(
        string password);

    bool VerifyPassword(
        string password,
        string passwordHash);
}
