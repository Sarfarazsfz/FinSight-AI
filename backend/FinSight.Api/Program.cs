using FinSight.Api.Authentication;
using FinSight.Api.ErrorHandling;
using FinSight.Api.Provisioning;
using FinSight.Application.Abstractions.Services;
using FinSight.Infrastructure;
using FinSight.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;

// Offline provisioning branch:
//
//     dotnet run -- create-user --email <email> --role <Admin|User>
//
// FinSight has no public registration endpoint by design, so the first
// account has to be created out-of-band. This runs and returns before any
// web-host wiring below -- the API is never started, and the running API
// never exposes provisioning. It is placed ahead of
// WebApplication.CreateBuilder deliberately: that call's command-line
// configuration provider rejects a bare token like "create-user", so the
// check cannot live after it.
//
// A plain `dotnet run` has args.Length == 0 and is completely unaffected.
if (UserProvisioningCommand.Matches(args))
{
    return await UserProvisioningCommand.RunAsync(args);
}

var builder =
    WebApplication.CreateBuilder(args);

// Register MVC controllers.
builder.Services.AddControllers();

// Register CORS for the Angular frontend. Allowed origins are entirely
// configuration-driven (Cors:AllowedOrigins) -- Development defaults to
// the Angular CLI's dev-server origin when no configuration is present.
// Never AllowAnyOrigin(); no AllowCredentials() since auth is a Bearer
// token in the Authorization header, not a cookie.
const string AngularClientCorsPolicy =
    "AngularClient";

var corsAllowedOrigins =
    builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>();

if (corsAllowedOrigins is null ||
    corsAllowedOrigins.Length == 0)
{
    corsAllowedOrigins =
        builder.Environment.IsDevelopment()
            ? new[] { "http://localhost:4200" }
            : Array.Empty<string>();
}

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        AngularClientCorsPolicy,
        policy =>
        {
            policy
                .WithOrigins(corsAllowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

// Register ProblemDetails support.
builder.Services.AddProblemDetails();

// Register global exception handler.
builder.Services.AddExceptionHandler<
    GlobalExceptionHandler>();

// Register OpenAPI support.
builder.Services.AddOpenApi(options =>
{
    var bearerScheme =
        new OpenApiSecurityScheme
        {
            Type =
                SecuritySchemeType.Http,

            Scheme =
                "bearer",

            BearerFormat =
                "JWT",

            In =
                ParameterLocation.Header,

            Description =
                "Enter a valid JWT bearer token."
        };

    options.AddDocumentTransformer(
        (document, context, cancellationToken) =>
        {
            document.Components ??=
                new OpenApiComponents();

            document.Components.SecuritySchemes ??=
                new Dictionary<string, IOpenApiSecurityScheme>();

            document.Components.SecuritySchemes["Bearer"] =
                bearerScheme;

            return Task.CompletedTask;
        });

    options.AddOperationTransformer(
        (operation, context, cancellationToken) =>
        {
            var hasAllowAnonymous =
                context.Description
                    .ActionDescriptor
                    .EndpointMetadata
                    .OfType<AllowAnonymousAttribute>()
                    .Any();

            if (hasAllowAnonymous)
            {
                operation.Security =
                    new List<OpenApiSecurityRequirement>();

                return Task.CompletedTask;
            }

            operation.Security =
                new List<OpenApiSecurityRequirement>
                {
                    new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("Bearer", context.Document, null)] = []
                    }
                };

            return Task.CompletedTask;
        });
});

// Bind JWT configuration from User Secrets / configuration.
var jwtOptions =
    builder.Configuration
        .GetSection(JwtOptions.SectionName)
        .Get<JwtOptions>()
    ?? throw new InvalidOperationException(
        "JWT configuration section 'Jwt' was not found.");

if (string.IsNullOrWhiteSpace(jwtOptions.Issuer))
{
    throw new InvalidOperationException(
        "JWT Issuer is required.");
}

if (string.IsNullOrWhiteSpace(jwtOptions.Audience))
{
    throw new InvalidOperationException(
        "JWT Audience is required.");
}

if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey))
{
    throw new InvalidOperationException(
        "JWT SecretKey is required.");
}

if (jwtOptions.ExpirationMinutes <= 0)
{
    throw new InvalidOperationException(
        "JWT ExpirationMinutes must be greater than zero.");
}

builder.Services.AddSingleton(jwtOptions);

// Register JWT bearer authentication.
builder.Services.AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,

                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            jwtOptions.SecretKey)),

                ValidateLifetime = true,

                ClockSkew =
                    TimeSpan.FromSeconds(30)
            };
    });

// Register authorization services.
builder.Services.AddAuthorization();

// Register Infrastructure services.
builder.Services.AddInfrastructure(
    builder.Configuration);

// Current-user identity. Registered here, not from AddInfrastructure --
// IHttpContextAccessor is a web-host concept, and AddInfrastructure is
// also consumed by the offline `create-user` command and the AI provider
// DI tests, neither of which has an HTTP context.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

var app =
    builder.Build();

// Configure centralized exception handling.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment() ||
    !string.IsNullOrWhiteSpace(
        builder.Configuration["ASPNETCORE_HTTPS_PORT"]))
{
    app.UseHttpsRedirection();
}

app.UseCors(AngularClientCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Required because the provisioning branch above returns an exit code,
// which makes this entry point int-returning. Reached only after the web
// host shuts down normally; it does not change runtime behavior.
return 0;

public partial class Program
{
}
