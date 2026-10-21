using Mapster;
using TourismHcm.Contracts.Admin;
using TourismHcm.Contracts.Audio;
using TourismHcm.Contracts.Favorites;
using TourismHcm.Contracts.Places;
using TourismHcm.Contracts.Reviews;
using TourismHcm.Contracts.Tours;
using TourismHcm.Domain.Entities;

namespace TourismHcm.Application.Mapping;

public sealed class MapsterRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Place, PlaceDto>()
            .Map(dest => dest.Description, src => src.DescriptionVi)
            .Map(dest => dest.Category, src => src.Category.ToString())
            .Map(dest => dest.Status, src => src.Status.ToString())
            .Map(dest => dest.Ward, src => src.District)
            .Map(dest => dest.Tags, src => SplitTags(src.Tags));

        config.NewConfig<Place, PlaceDetailDto>()
            .Inherits<Place, PlaceDto>()
            .Map(dest => dest.Translations, src => src.Translations.Adapt<IReadOnlyList<PlaceTranslationDto>>())
            .Ignore(dest => dest.AvailableAudioLanguages);

        // Without an explicit config, Mapster does NOT reuse the Place->PlaceDto mapping (e.g.
        // the Tags string -> string[] split) for other DTOs derived from PlaceDto; it falls back
        // to its default converter, which cannot convert a raw string into a string[] and throws
        // at runtime. Place->NearbyPlaceDto needs the same base mapping, plus the extra
        // DistanceMeters field computed by PlaceService.
        config.NewConfig<Place, NearbyPlaceDto>()
            .Inherits<Place, PlaceDto>();

        config.NewConfig<PlaceTranslation, PlaceTranslationDto>()
            .Map(dest => dest.Source, src => src.Source.ToString());

        config.NewConfig<Review, ReviewDto>()
            .Map(dest => dest.UserDisplayName, src => src.User != null ? src.User.DisplayName : string.Empty);

        config.NewConfig<Favorite, FavoriteDto>()
            .Map(dest => dest.PlaceName, src => src.Place != null ? src.Place.Name : string.Empty)
            .Map(dest => dest.ImageUrl, src => src.Place != null ? src.Place.ImageUrl : string.Empty)
            .Map(dest => dest.Latitude, src => src.Place != null ? src.Place.Latitude : 0)
            .Map(dest => dest.Longitude, src => src.Place != null ? src.Place.Longitude : 0);

        config.NewConfig<AuditLog, AuditLogDto>();

        config.NewConfig<AudioGenerationTask, AudioTaskDto>()
            .Map(dest => dest.Status, src => src.Status.ToString());

        config.NewConfig<TourStop, TourStopDto>()
            .Map(dest => dest.PlaceName, src => src.Place != null ? src.Place.Name : string.Empty)
            .Map(dest => dest.Latitude, src => src.Place != null ? src.Place.Latitude : 0)
            .Map(dest => dest.Longitude, src => src.Place != null ? src.Place.Longitude : 0);

        config.NewConfig<Tour, TourDto>()
            .Map(dest => dest.Stops, src => src.Stops
                .OrderBy(s => s.Order)
                .ToList()
                .Adapt<IReadOnlyList<TourStopDto>>());
    }

    private static string[] SplitTags(string tags) =>
        string.IsNullOrWhiteSpace(tags)
            ? Array.Empty<string>()
            : tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
