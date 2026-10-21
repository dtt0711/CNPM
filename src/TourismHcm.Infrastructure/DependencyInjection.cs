using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Options;
using TourismHcm.Domain.Constants;
using TourismHcm.Infrastructure.Auth;
using TourismHcm.Infrastructure.Persistence;
using TourismHcm.Infrastructure.Providers.Ai;
using TourismHcm.Infrastructure.Providers.Audio;
using TourismHcm.Infrastructure.Providers.Security;
using TourismHcm.Infrastructure.Providers.TextToSpeech;
using TourismHcm.Infrastructure.Providers.Translation;
using TourismHcm.Infrastructure.Repositories;

namespace TourismHcm.Infrastructure;

/// <summary>
/// Single composition-root entry point for every Infrastructure-layer dependency: persistence,
/// repositories, caching, external provider integrations, background workers, and JWT
/// authentication/authorization. This is the ONLY file the Api project is expected to invoke
/// (via <see cref="AddInfrastructure"/>) to wire up Infrastructure services.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        AddOptions(services, configuration, environment);
        AddPersistence(services, configuration);
        AddCaching(services, configuration, environment);
        AddRepositories(services);
        AddProviders(services, configuration, environment);
        AddAudioPipeline(services);
        AddAuth(services, configuration);

        services.AddHttpContextAccessor();

        return services;
    }

    private static void AddOptions(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AudioOptions>(configuration.GetSection(AudioOptions.SectionName));
        services.Configure<AudioStorageOptions>(configuration.GetSection(AudioStorageOptions.SectionName));
        services.Configure<GeofenceOptions>(configuration.GetSection(GeofenceOptions.SectionName));
        services.Configure<SpeechOptions>(configuration.GetSection(SpeechOptions.SectionName));
        services.Configure<TranslationOptions>(configuration.GetSection(TranslationOptions.SectionName));
        services.Configure<ProvidersOptions>(configuration.GetSection(ProvidersOptions.SectionName));
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.AddOptions<MapPackOptions>()
            .Bind(configuration.GetSection(MapPackOptions.SectionName))
            .PostConfigure(opts =>
            {
                if (string.IsNullOrWhiteSpace(opts.DataDir))
                {
                    opts.DataDir = Path.Combine(environment.ContentRootPath, "wwwroot", "static", "maps");
                    return;
                }

                if (!Path.IsPathRooted(opts.DataDir))
                {
                    opts.DataDir = Path.GetFullPath(Path.Combine(environment.ContentRootPath, opts.DataDir));
                }
            });
        services.Configure<PiiOptions>(configuration.GetSection(PiiOptions.SectionName));
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? "Server=.;Database=TourismHcm;Trusted_Connection=True;TrustServerCertificate=True;";

        services.AddDbContext<TourismHcmDbContext>(options => options.UseSqlServer(connectionString));
    }

    /// <summary>
    /// Registers the <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>
    /// implementation used for geofence trigger cooldowns. Testing always gets an in-memory
    /// cache (fast, isolated, no external dependency). Development prefers Redis when a
    /// connection string is configured AND reachable, falling back to in-memory otherwise so
    /// local development never hard-fails on a missing Redis instance. All other environments
    /// (e.g. Production) use Redis as configured.
    /// </summary>
    private static void AddCaching(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsEnvironment("Testing"))
        {
            services.AddDistributedMemoryCache();
            return;
        }

        var redisConnectionString = configuration.GetConnectionString("Redis");

        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddDistributedMemoryCache();
            return;
        }

        if (environment.IsDevelopment() && !IsRedisReachable(redisConnectionString))
        {
            services.AddDistributedMemoryCache();
            return;
        }

        services.AddStackExchangeRedisCache(options => options.Configuration = redisConnectionString);
    }

    private static bool IsRedisReachable(string connectionString)
    {
        try
        {
            var options = ConfigurationOptions.Parse(connectionString);
            options.ConnectTimeout = 500;
            options.AbortOnConnectFail = true;

            using var connection = ConnectionMultiplexer.Connect(options);
            return connection.IsConnected;
        }
        catch
        {
            return false;
        }
    }

    private static void AddRepositories(IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPlaceRepository, PlaceRepository>();
        services.AddScoped<ITranslationRepository, TranslationRepository>();
        services.AddScoped<IAudioAssetRepository, AudioAssetRepository>();
        services.AddScoped<IAudioTaskRepository, AudioTaskRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IFavoriteRepository, FavoriteRepository>();
        services.AddScoped<ITourRepository, TourRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IPlatformRepository, PlatformRepository>();
    }

    /// <summary>
    /// Registers translation/TTS providers and local disk audio storage.
    ///
    /// Real providers are registered in Development and Production (sidecar Free/Edge by default).
    /// Fake stubs exist only for the Testing host so unit/integration tests never need the sidecar.
    /// </summary>
    private static void AddProviders(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var providersBaseUrl = (configuration[$"{ProvidersOptions.SectionName}:BaseUrl"] ?? "http://localhost:8090")
            .TrimEnd('/');
        var timeoutSeconds = int.TryParse(configuration[$"{ProvidersOptions.SectionName}:TimeoutSeconds"], out var t)
            ? t
            : 60;

        void ConfigureSidecar(HttpClient client)
        {
            client.BaseAddress = new Uri(providersBaseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        }

        // Real providers only. Fake stubs are allowed exclusively in the Testing environment
        // (unit/integration test hosts) so CI never depends on network sidecars.
        if (environment.IsEnvironment("Testing"))
        {
            services.AddSingleton<ITranslationProvider, FakeTranslationProvider>();
            services.AddSingleton<ITextToSpeechProvider, FakeTextToSpeechProvider>();
        }
        else
        {
            var translationProvider = (configuration[$"{TranslationOptions.SectionName}:Provider"] ?? "Free").Trim();
            if (string.Equals(translationProvider, "Google", StringComparison.OrdinalIgnoreCase))
            {
                services.AddSingleton<ITranslationProvider, GoogleTranslationProvider>();
            }
            else
            {
                // Free (sidecar) is the default; "Fake" config is ignored and mapped to Free.
                services.AddHttpClient<ITranslationProvider, FreeGoogleTranslationProvider>(ConfigureSidecar);
            }

            var speechProvider = (configuration[$"{SpeechOptions.SectionName}:Provider"] ?? "Edge").Trim();
            if (string.Equals(speechProvider, "Azure", StringComparison.OrdinalIgnoreCase))
            {
                services.AddSingleton<ITextToSpeechProvider, AzureSpeechTtsProvider>();
            }
            else
            {
                // Edge (sidecar) is the default; "Fake" config is ignored and mapped to Edge.
                services.AddHttpClient<ITextToSpeechProvider, EdgeTtsProvider>(ConfigureSidecar);
            }
        }

        services.AddSingleton<IAudioStorage, LocalDiskAudioStorage>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddSingleton<IPiiProtector, AesGcmPiiProtector>();
        services.AddHttpClient<IAiAdvisorProvider, GeminiAiAdvisorProvider>();
    }

    private static void AddAudioPipeline(IServiceCollection services)
    {
        services.AddSingleton<ChannelAudioTaskQueue>();
        services.AddSingleton<IAudioTaskQueue>(sp => sp.GetRequiredService<ChannelAudioTaskQueue>());
        services.AddSingleton<IAudioTaskRuntime, AudioTaskRuntime>();
        services.AddHostedService<AudioGenerationBackgroundService>();
        services.AddHostedService<PiiRedactionBackgroundService>();
    }

    private static void AddAuth(IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var issuer = jwtSection["Issuer"] ?? "TourismHcm";
        var audience = jwtSection["Audience"] ?? "TourismHcm.Client";
        var signingKey = jwtSection["SigningKey"] ?? string.Empty;

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        if (string.IsNullOrEmpty(ctx.Token)
                            && ctx.Request.Cookies.TryGetValue("access_token", out var token))
                        {
                            ctx.Token = token;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddSingleton<IAuthorizationHandler, PermissionHandler>();

        var authBuilder = services.AddAuthorizationBuilder()
            .AddPolicy("RequireAdmin", policy => policy.RequireRole(AppRoles.Admin, AppRoles.SuperAdmin))
            .AddPolicy("RequireEditor", policy => policy.RequireRole(
                AppRoles.Admin, AppRoles.SuperAdmin, AppRoles.EditorLegacyName));

        // Dynamic RBAC: one policy per static permission (JWT claim checked by PermissionHandler).
        foreach (var permission in AppPermissions.All)
        {
            var policyName = $"Permission:{permission}";
            authBuilder.AddPolicy(policyName, policy =>
                policy.Requirements.Add(new PermissionRequirement(permission)));
        }
    }
}
