using System.Text;
using FinSight.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Api.Provisioning;

/// <summary>
/// The <c>create-user</c> CLI entry point:
///
///     dotnet run -- create-user --email operator@example.com --role User
///
/// Invoked from Program.cs *before* any web-host wiring, and always
/// returns without starting Kestrel -- normal `dotnet run` never reaches
/// this code, and the running API never exposes provisioning.
///
/// The password is never an argument. Command-line arguments are visible
/// in shell history and to any process that can read the process list, so
/// the password is read either from a hidden interactive prompt (keys are
/// intercepted, nothing is echoed) or, for non-interactive automation,
/// from the FINSIGHT_PROVISION_PASSWORD environment variable. Neither the
/// password nor the resulting hash is ever printed.
/// </summary>
public static class UserProvisioningCommand
{
    public const string CommandName = "create-user";

    public const string PasswordEnvironmentVariable =
        "FINSIGHT_PROVISION_PASSWORD";

    private const int UsageExitCode = 1;

    /// <summary>
    /// True only when the first argument is exactly the command name, so
    /// no ordinary startup argument can accidentally trigger provisioning.
    /// </summary>
    public static bool Matches(string[] args) =>
        args.Length > 0 &&
        string.Equals(args[0], CommandName, StringComparison.Ordinal);

    public static async Task<int> RunAsync(string[] args)
    {
        if (!TryParseOption(args, "--email", out var email) ||
            !TryParseOption(args, "--role", out var role))
        {
            WriteUsage();
            return UsageExitCode;
        }

        var password = ReadPassword();

        if (password is null)
        {
            // ReadPassword already explained why.
            return UsageExitCode;
        }

        try
        {
            // The builder is used ONLY as a configuration source. Empty
            // args deliberately: WebApplicationBuilder's command-line
            // provider rejects a bare token like "create-user", and
            // provisioning takes no configuration from argv anyway.
            // Resolving configuration this way still picks up
            // appsettings, user-secrets and environment variables exactly
            // as the real API does, so the connection string a developer
            // configured by following the setup doc is the one used here.
            var configuration =
                WebApplication
                    .CreateBuilder(Array.Empty<string>())
                    .Configuration;

            // A private container rather than builder.Build(): the web
            // host validates every registered descriptor on build in
            // Development, which would demand JwtOptions -- a value only
            // the normal startup path registers, and one provisioning has
            // no use for. Provisioning must not require JWT configuration
            // to create a user, so it builds only what it actually needs.
            var services = new ServiceCollection();

            services.AddInfrastructure(configuration);
            services.AddScoped<UserProvisioningService>();

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            var service =
                scope.ServiceProvider
                    .GetRequiredService<UserProvisioningService>();

            var result =
                await service.ProvisionAsync(email, role, password);

            if (result.IsSuccess)
            {
                Console.WriteLine(result.Message);
                Console.WriteLine(
                    "You can now sign in through the application's login page.");
            }
            else
            {
                Console.Error.WriteLine($"Provisioning failed: {result.Message}");
            }

            return result.ExitCode;
        }
        catch (Exception ex)
        {
            // Message only -- never the stack, never any configuration
            // value, and never the password or hash.
            Console.Error.WriteLine($"Provisioning failed: {ex.Message}");
            return UsageExitCode;
        }
    }

    /// <summary>
    /// Reads "--name value" from the argument list. Returns false when the
    /// option is absent or has no value, which the caller turns into usage
    /// output rather than a null-reference later on.
    /// </summary>
    private static bool TryParseOption(
        string[] args,
        string name,
        out string value)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.Ordinal))
            {
                value = args[i + 1];
                return !string.IsNullOrWhiteSpace(value);
            }
        }

        value = string.Empty;
        return false;
    }

    /// <summary>
    /// Environment variable first (the documented automation path), then a
    /// hidden interactive prompt with confirmation. Returns null when no
    /// password could be obtained safely -- notably when stdin is
    /// redirected, where Console.ReadKey cannot suppress echo and silently
    /// reading the password would risk leaking it into a log.
    /// </summary>
    private static string? ReadPassword()
    {
        var fromEnvironment =
            Environment.GetEnvironmentVariable(PasswordEnvironmentVariable);

        if (!string.IsNullOrEmpty(fromEnvironment))
        {
            return fromEnvironment;
        }

        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine(
                "Cannot prompt for a password because input is redirected. " +
                $"Set the {PasswordEnvironmentVariable} environment variable instead.");

            return null;
        }

        var first = ReadHiddenLine("Password: ");

        if (first.Length == 0)
        {
            Console.Error.WriteLine("Password cannot be empty.");
            return null;
        }

        var second = ReadHiddenLine("Confirm password: ");

        if (!string.Equals(first, second, StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Passwords did not match.");
            return null;
        }

        return first;
    }

    /// <summary>
    /// Reads a line with echo suppressed. Backspace is supported so a
    /// typo does not force restarting the command; no character is ever
    /// written to the console, not even a masking asterisk, so the
    /// password length is not disclosed to anyone watching.
    /// </summary>
    private static string ReadHiddenLine(string prompt)
    {
        Console.Write(prompt);

        var buffer = new StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return buffer.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                {
                    buffer.Length--;
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                buffer.Append(key.KeyChar);
            }
        }
    }

    private static void WriteUsage()
    {
        Console.Error.WriteLine(
            "Usage: dotnet run -- create-user --email <email> --role <Admin|User>");
        Console.Error.WriteLine();
        Console.Error.WriteLine(
            "The password is never passed as an argument. You will be prompted for it,");
        Console.Error.WriteLine(
            $"or you may set the {PasswordEnvironmentVariable} environment variable.");
    }
}
