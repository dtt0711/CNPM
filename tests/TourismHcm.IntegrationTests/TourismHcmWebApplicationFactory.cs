using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Contracts.Auth;
using TourismHcm.Contracts.Places;
using TourismHcm.Domain.Constants;
using TourismHcm.Domain.Entities;
using TourismHcm.Infrastructure.Persistence;
using TourismHcm.Infrastructure.Seed;

namespace TourismHcm.IntegrationTests;

/// <summary>
/// Boots the API with <c>ASPNETCORE_ENVIRONMENT=Testing</c> against a Testcontainers SQL Server.
/// When Docker or the container cannot be reached, <see cref="DatabaseAvailable"/> stays false and
/// tests call <see cref="Skip"/>.If so the run is reported as skipped rather than failed.
/// </summary>
public sealed class TourismHcmWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@tourismhcm.test";
    public const string AdminPassword = "AdminPass123!";

    private const string SigningKey = "integration-test-signing-key-32b!!";

    private readonly SemaphoreSlim _seedLock = new(1, 1);
    private MsSqlContainer? _sql;
    private string? _connectionString;
    private bool _seeded;
    private readonly Dictionary<string, string?> _previousEnv = [];

    public bool DatabaseAvailable { get; private set; }

    public string UnavailableReason { get; private set; } = "SQL Server container is not available.";

    public async Task InitializeAsync()
    {
        try
        {
            _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            using var startup = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            await _sql.StartAsync(startup.Token);
            _connectionString = _sql.GetConnectionString();
            // WebApplication.CreateBuilder reads env vars before AddInfrastructure captures
            // ConnectionStrings:Default. appsettings.json stores "" so the in-memory WAF
            // overlay is too late unless these are set first.
            SetEnvironment("ConnectionStrings__Default", _connectionString);
            SetEnvironment("Jwt__SigningKey", SigningKey);
            SetEnvironment("Admin__Email", AdminEmail);
            SetEnvironment("Admin__Password", AdminPassword);
            DatabaseAvailable = true;
        }
        catch (Exception ex)
        {
            DatabaseAvailable = false;
            UnavailableReason = ex.Message;
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        if (_sql is not null)
        {
            await _sql.DisposeAsync();
        }

        foreach (var (key, value) in _previousEnv)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    public async Task<HttpClient> CreateReadyClientAsync()
    {
        Skip.If(!DatabaseAvailable, UnavailableReason);

        var client = CreateClient();
        client.Timeout = TimeSpan.FromMinutes(2);
        await EnsureSeededAsync();
        return client;
    }

    public async Task GrantRoleAsync(string email, string roleName)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TourismHcmDbContext>();
        var normalized = email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(u => u.UserRoles).SingleAsync(u => u.Email == normalized);
        var role = await db.Roles.SingleAsync(r => r.Name == roleName);

        if (user.UserRoles.All(ur => ur.RoleId != role.Id))
        {
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            await db.SaveChangesAsync();
        }
    }

    public async Task<Place> RequirePlaceAsync(Guid placeId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TourismHcmDbContext>();
        return await db.Places.AsNoTracking().SingleAsync(p => p.Id == placeId);
    }

    public async Task<IReadOnlyList<string>> GetAudioContentHashesAsync(Guid placeId, string lang)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TourismHcmDbContext>();
        return await db.AudioGenerationTasks.AsNoTracking()
            .Where(t => t.PlaceId == placeId && t.Lang == lang)
            .Select(t => t.ContentHash)
            .ToListAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _connectionString ?? string.Empty);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Admin:Email", AdminEmail);
        builder.UseSetting("Admin:Password", AdminPassword);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _connectionString,
                ["ConnectionStrings:Redis"] = "",
                ["Jwt:Issuer"] = "TourismHcm",
                ["Jwt:Audience"] = "TourismHcm.Client",
                ["Jwt:SigningKey"] = SigningKey,
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7",
                ["Admin:Email"] = AdminEmail,
                ["Admin:Password"] = AdminPassword,
                ["Translation:ApiKey"] = "",
                ["Speech:AzureKey"] = "",
                ["Speech:AzureRegion"] = "",
                ["AudioStorage:RootPath"] = Path.Combine(Path.GetTempPath(), "tourismhcm-integration-audio"),
                ["AudioStorage:PublicBaseUrl"] = "/audio",
            });
        });
        builder.ConfigureServices(services =>
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                return;
            }

            services.RemoveAll<DbContextOptions<TourismHcmDbContext>>();
            services.AddDbContext<TourismHcmDbContext>(options => options.UseSqlServer(_connectionString));
        });
    }

    private void SetEnvironment(string key, string value)
    {
        if (!_previousEnv.ContainsKey(key))
        {
            _previousEnv[key] = Environment.GetEnvironmentVariable(key);
        }

        Environment.SetEnvironmentVariable(key, value);
    }

    private async Task EnsureSeededAsync()
    {
        if (_seeded)
        {
            return;
        }

        await _seedLock.WaitAsync();
        try
        {
            if (_seeded)
            {
                return;
            }

            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TourismHcmDbContext>();

            // Program.cs calls MigrateAsync, but this repo has no EF migrations, so that only
            // creates an empty database. Rebuild the schema from the current model, then seed.
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();

            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<TourismHcmWebApplicationFactory>>();
            await DataSeeder.SeedAsync(db, configuration, passwordHasher, logger);

            _seeded = true;
        }
        finally
        {
            _seedLock.Release();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<TourismHcmWebApplicationFactory>
{
    public const string Name = "TourismHcm.Integration";
}

internal static class Api
{
    public static async Task<AuthResponse> RegisterAsync(HttpClient client, string? email = null, string password = "Password123")
    {
        email ??= $"user-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
        {
            Email = email,
            Password = password,
            DisplayName = "Integration User",
        });

        return await ReadAsync<AuthResponse>(response, HttpStatusCode.OK);
    }

    public static async Task<AuthResponse> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Email = email,
            Password = password,
        });

        return await ReadAsync<AuthResponse>(response, HttpStatusCode.OK);
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(expected, body);
        return JsonSerializer.Deserialize<T>(body, JsonSerializerOptions.Web)!;
    }

    public static void UseBearer(this HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    public static CreatePlaceRequest SamplePlace(string? name = null, string? descriptionVi = null) => new()
    {
        Name = name ?? $"Place {Guid.NewGuid():N}",
        DescriptionVi = descriptionVi ?? "Mô tả địa điểm dùng cho kiểm thử tích hợp của ứng dụng du lịch.",
        Category = "Landmark",
        Ward = "Bến Nghé",
        Address = "1 Nguyen Hue, Phường Bến Nghé",
        Latitude = 10.7769,
        Longitude = 106.7009,
        TriggerRadiusMeters = 40,
        OpeningHours = "08:00-17:00",
        TicketPrice = "Free",
        ImageUrl = "https://example.com/place.jpg",
        Tags = "integration,test",
    };

    public static async Task<HttpClient> CreateAdminClientAsync(this TourismHcmWebApplicationFactory factory)
    {
        var client = await factory.CreateReadyClientAsync();
        var auth = await LoginAsync(client, TourismHcmWebApplicationFactory.AdminEmail, TourismHcmWebApplicationFactory.AdminPassword);
        auth.Roles.Should().Contain(AppRoles.Admin);
        client.UseBearer(auth.AccessToken);
        return client;
    }
}
