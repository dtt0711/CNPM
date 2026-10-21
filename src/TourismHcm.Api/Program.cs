using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Serilog;
using TourismHcm.Api.HealthChecks;
using TourismHcm.Api.Middleware;
using TourismHcm.Application;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Options;
using TourismHcm.Infrastructure;
using TourismHcm.Infrastructure.Persistence;
using TourismHcm.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

// ---------- Logging ----------
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// ---------- Application + Infrastructure (the ONLY place Infrastructure is referenced for DI) ----------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

// ---------- MVC / Controllers ----------
builder.Services.AddControllers();

// ---------- API Versioning ----------
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddMvc().AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// ---------- Swagger ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "TourismHcm API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT bearer token, e.g. \"Bearer {token}\".",
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>(),
    });
});

// ---------- CORS ----------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }
        else if (builder.Environment.IsDevelopment())
        {
            // Dev convenience: allow any origin with credentials (cookie auth + Vite).
            policy.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }
        else
        {
            // Safe default in non-Development environments without explicit configuration:
            // no cross-origin browser access until Cors:AllowedOrigins is set.
            policy.WithOrigins(Array.Empty<string>());
        }
    });
});

// ---------- Rate limiting ----------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientPartitionKey(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Stricter named policy for expensive TTS/translation-triggering endpoints.
    options.AddPolicy("tts", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientPartitionKey(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

// ---------- Health checks ----------
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Server=.;Database=TourismHcm;Trusted_Connection=True;TrustServerCertificate=True;";
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");

var healthChecks = builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "sql", tags: ["ready"])
    .AddCheck<AudioStorageHealthCheck>("audio-storage", tags: ["ready"]);

if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    // Redis is optional in Development: only wired into readiness when actually configured.
    healthChecks.AddRedis(redisConnectionString, name: "redis", tags: ["ready"]);
}

var app = builder.Build();

// ---------- Static files for generated narration audio ----------
var audioStorageOptions = app.Services.GetRequiredService<IOptions<AudioStorageOptions>>().Value;
var audioRoot = Path.IsPathRooted(audioStorageOptions.RootPath)
    ? audioStorageOptions.RootPath
    : Path.Combine(AppContext.BaseDirectory, audioStorageOptions.RootPath);
Directory.CreateDirectory(audioRoot);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(audioRoot),
    RequestPath = audioStorageOptions.PublicBaseUrl,
});

// ---------- Static map packs (PMTiles / styles / fonts) ----------
var mapPackOptions = app.Services.GetRequiredService<IOptions<MapPackOptions>>().Value;
var mapRoot = Path.IsPathRooted(mapPackOptions.DataDir)
    ? mapPackOptions.DataDir
    : Path.Combine(app.Environment.ContentRootPath, mapPackOptions.DataDir);
Directory.CreateDirectory(mapRoot);

// .pmtiles is not in the default MIME map — without this, StaticFiles returns 404
// even when the file exists on disk.
var mapContentTypes = new FileExtensionContentTypeProvider();
mapContentTypes.Mappings[".pmtiles"] = "application/vnd.pmtiles";
mapContentTypes.Mappings[".pbf"] = "application/x-protobuf";

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(mapRoot),
    RequestPath = "/static/maps",
    ContentTypeProvider = mapContentTypes,
    OnPrepareResponse = ctx =>
    {
        var path = ctx.File.Name;
        if (path.EndsWith(".pmtiles", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".pbf", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            // PMTiles clients use HTTP Range requests
            ctx.Context.Response.Headers.AcceptRanges = "bytes";
        }
        else if (path.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "no-cache";
        }
    },
});

// Also serve default wwwroot (styles committed under wwwroot/static/maps).
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = mapContentTypes,
});

// ---------- Database bootstrap (never auto-migrate in Production) ----------
if (!app.Environment.IsProduction())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<TourismHcmDbContext>();

    try
    {
        await dbContext.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Database migration failed/skipped on startup (environment: {Environment}).", app.Environment.EnvironmentName);
    }

    if (app.Environment.IsDevelopment())
    {
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var seedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            await DataSeeder.SeedAsync(dbContext, configuration, passwordHasher, seedLogger);
        }
        catch (Exception ex)
        {
            seedLogger.LogWarning(ex, "Development data seeding failed (likely no database connection available).");
        }
    }
}

// ---------- Middleware pipeline ----------
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// http-only Development profile has no HTTPS port; skip redirect to avoid startup warnings.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("Default");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    // API has no home page; redirect `/` to Swagger UI for local browsing.
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

app.Run();

static string GetClientPartitionKey(HttpContext httpContext) =>
    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

/// <summary>
/// Entry point class, exposed so test hosts (e.g. <c>WebApplicationFactory&lt;Program&gt;</c>)
/// can bootstrap the application in-process.
/// </summary>
public sealed partial class Program
{
}
