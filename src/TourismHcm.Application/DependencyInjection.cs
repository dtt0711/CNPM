using FluentValidation;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Application.Services;

namespace TourismHcm.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        AddMapster(services);
        AddValidators(services);
        AddServices(services);

        // Enables IOptions<T> resolution with sensible defaults for every Options class in this
        // assembly even before the host binds configuration sections (e.g. in unit tests).
        services.AddOptions();

        return services;
    }

    private static void AddMapster(IServiceCollection services)
    {
        var config = TypeAdapterConfig.GlobalSettings;
        config.Scan(typeof(DependencyInjection).Assembly);

        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();
    }

    private static void AddValidators(IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
    }

    private static void AddServices(IServiceCollection services)
    {
        services.AddScoped<IPlaceService, PlaceService>();
        services.AddScoped<IGeofenceService, GeofenceService>();
        services.AddScoped<INarrationService, NarrationService>();
        services.AddScoped<IAudioService, AudioService>();
        services.AddScoped<IOfflinePackService, OfflinePackService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IFavoriteService, FavoriteService>();
        services.AddScoped<ITourService, TourService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<ILanguageService, LanguageService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IAiAdvisorService, AiAdvisorService>();
        services.AddScoped<IMapPackService, MapPackService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IUiBundleService, UiBundleService>();
        services.AddScoped<ILocalizationService, LocalizationService>();
    }
}
