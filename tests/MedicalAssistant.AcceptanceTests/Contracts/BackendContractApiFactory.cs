using System.Security.Claims;
using System.Text.Encodings.Web;
using MediatR;
using MedicalAssistant.Application.Features.Chat.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace MedicalAssistant.AcceptanceTests.Contracts;

/// <summary>
/// Starts the real Backend API routing pipeline without external infrastructure.
/// Only process-hosting and identity persistence are replaced; controllers,
/// model binding, authentication middleware, OpenAPI, and SignalR stay real.
/// </summary>
public sealed class BackendContractApiFactory : WebApplicationFactory<Program>
{
    internal const string AuthenticationScheme = "BackendContract";
    internal const string DoctorIdHeader = "X-Contract-Doctor-Id";
    internal const string CallbackApiKey = "backend-contract-callback-key";
    private readonly IChatProgressNotifier? _chatProgress;

    public BackendContractApiFactory()
    {
    }

    internal BackendContractApiFactory(IChatProgressNotifier chatProgress)
    {
        _chatProgress = chatProgress;
    }

    public Mock<IMediator> Mediator { get; } = new(MockBehavior.Strict);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("ContractTests");
        builder.UseSetting("https_port", "443");
        builder.UseSetting("Database:Provider", "PostgreSQL");
        builder.UseSetting(
            "ConnectionStrings:MedicalAssistantDatabasePostgreSQL",
            "Host=localhost;Port=5432;Database=contract_tests;Username=contract;Password=contract");
        builder.UseSetting("JwtSettings:Key", "contract-tests-only-signing-key-that-is-long-enough");
        builder.UseSetting("JwtSettings:Issuer", "contract-tests");
        builder.UseSetting("JwtSettings:Audience", "contract-tests");
        builder.UseSetting("AiCallback:ApiKey", CallbackApiKey);
        builder.UseSetting("RabbitMQ:HostName", "localhost");
        builder.UseSetting("RabbitMQ:Port", "5672");
        builder.UseSetting("RabbitMQ:UserName", "guest");
        builder.UseSetting("RabbitMQ:Password", "guest");
        builder.UseSetting("ConsultationOutboxRelay:Enabled", "false");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();

            services.RemoveAll<IMediator>();
            services.AddSingleton(Mediator.Object);

            if (_chatProgress is not null)
            {
                services.RemoveAll<IChatProgressNotifier>();
                services.AddSingleton(_chatProgress);
            }

            services.RemoveAll<RoleManager<IdentityRole>>();
            services.AddSingleton(CreateRoleManager());

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = AuthenticationScheme;
                    options.DefaultChallengeScheme = AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, ContractAuthenticationHandler>(
                    AuthenticationScheme,
                    _ => { });
        });
    }

    private static RoleManager<IdentityRole> CreateRoleManager()
    {
        var manager = new Mock<RoleManager<IdentityRole>>(
            MockBehavior.Loose,
            Mock.Of<IRoleStore<IdentityRole>>(),
            Array.Empty<IRoleValidator<IdentityRole>>(),
            Mock.Of<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            Mock.Of<ILogger<RoleManager<IdentityRole>>>());
        manager.Setup(candidate => candidate.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        return manager.Object;
    }
}

internal sealed class ContractAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public ContractAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var doctorId = Request.Headers[BackendContractApiFactory.DoctorIdHeader].FirstOrDefault()
            ?? "contract-doctor";
        Claim[] claims =
        [
            new("uid", doctorId),
            new(ClaimTypes.NameIdentifier, doctorId),
            new(ClaimTypes.Role, "Doctor"),
        ];
        var identity = new ClaimsIdentity(claims, BackendContractApiFactory.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, BackendContractApiFactory.AuthenticationScheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
