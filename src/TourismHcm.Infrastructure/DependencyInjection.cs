using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
using TourismHcm.Infrastructure.Persistence;
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
        AddOptions(services, configuration);
        AddPersistence(services, configuration);
        AddCaching(services, configuration, environment);
        AddRepositories(services);
        AddProviders(services, configuration, environment);
        AddAudioPipeline(services);
        AddAuth(services, configuration);

        services.AddHttpContextAccessor();

        return services;
    }

    private static void AddOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AudioOptions>(configuration.GetSection(AudioOptions.SectionName));
        services.Configure<AudioStorageOptions>(configuration.GetSection(AudioStorageOptions.SectionName));
        services.Configure<GeofenceOptions>(configuration.GetSection(GeofenceOptions.SectionName));
        services.Configure<SpeechOptions>(configuration.GetSection(SpeechOptions.SectionName));
        services.Configure<TranslationOptions>(configuration.GetSection(TranslationOptions.SectionName));
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
    }

    /// <summary>
    /// Registers translation/TTS providers and local disk audio storage.
    ///
    /// Rule: when a provider's API key/credentials are present, the real provider is always
    /// registered-regardless of environment-so staging/local setups with real keys exercise
    /// the real integration. When credentials are absent:
    ///   - Development/Testing: register the Fake provider stub (no external dependency).
    ///   - Any other environment (e.g. Production): still register the real provider class.
    ///     Its <c>IsConfigured</c> will be false and any usage throws
    ///     <see cref="TourismHcm.Domain.Exceptions.ProviderUnavailableException"/>, which the API
    ///     maps to HTTP 503-never silently falling back to fake audio/translations in Production.
    /// </summary>
    private static void AddProviders(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var isDevOrTest = environment.IsDevelopment() || environment.IsEnvironment("Testing");

        var translationApiKey = configuration[$"{TranslationOptions.SectionName}:ApiKey"];
        if (!string.IsNullOrWhiteSpace(translationApiKey))
        {
            services.AddSingleton<ITranslationProvider, GoogleTranslationProvider>();
        }
        else if (isDevOrTest)
        {
            services.AddSingleton<ITranslationProvider, FakeTranslationProvider>();
        }
        else
        {
            services.AddSingleton<ITranslationProvider, GoogleTranslationProvider>();
        }

        var speechKey = configuration[$"{SpeechOptions.SectionName}:AzureKey"];
        var speechRegion = configuration[$"{SpeechOptions.SectionName}:AzureRegion"];
        var speechConfigured = !string.IsNullOrWhiteSpace(speechKey) && !string.IsNullOrWhiteSpace(speechRegion);

        if (speechConfigured)
        {
            services.AddSingleton<ITextToSpeechProvider, AzureSpeechTtsProvider>();
        }
        else if (isDevOrTest)
        {
            services.AddSingleton<ITextToSpeechProvider, FakeTextToSpeechProvider>();
        }
        else
        {
            services.AddSingleton<ITextToSpeechProvider, AzureSpeechTtsProvider>();
        }

        services.AddSingleton<IAudioStorage, LocalDiskAudioStorage>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ICurrentUser, CurrentUser>();
    }

    private static void AddAudioPipeline(IServiceCollection services)
    {
        services.AddSingleton<ChannelAudioTaskQueue>();
        services.AddSingleton<IAudioTaskQueue>(sp => sp.GetRequiredService<ChannelAudioTaskQueue>());
        services.AddHostedService<AudioGenerationBackgroundService>();
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
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("RequireAdmin", policy => policy.RequireRole(AppRoles.Admin))
            .AddPolicy("RequireEditor", policy => policy.RequireRole(AppRoles.Admin, AppRoles.Editor));
    }
}
