namespace TourismHcm.Application.Options;

public sealed class MapPackOptions
{
    public const string SectionName = "MapPack";

    /// <summary>Root directory for static map packs (PMTiles, styles, fonts).</summary>
    public string DataDir { get; set; } = "wwwroot/static/maps";

    public string DefaultScope { get; set; } = "hcm";
}
