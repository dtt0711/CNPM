using System.Runtime.CompilerServices;
using Mapster;

namespace TourismHcm.UnitTests.TestDoubles;

/// <summary>
/// Unit tests exercise Application services directly (without going through
/// <c>TourismHcm.Application.DependencyInjection.AddApplication</c>), so the custom Mapster
/// configuration in <c>MapsterRegister</c> (e.g. null-safe navigation flattening for
/// <c>Review.User.DisplayName</c>, <c>Favorite.Place.*</c>, <c>TourStop.Place.*</c>) would never
/// be registered otherwise. A module initializer guarantees it runs exactly once, before any
/// test in this assembly, mirroring what the real composition root does at startup.
/// </summary>
internal static class MapsterTestSetup
{
    [ModuleInitializer]
    public static void Initialize()
    {
        TypeAdapterConfig.GlobalSettings.Scan(typeof(TourismHcm.Application.DependencyInjection).Assembly);
    }
}
