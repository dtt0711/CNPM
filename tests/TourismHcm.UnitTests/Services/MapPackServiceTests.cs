using FluentAssertions;
using TourismHcm.Application.Options;
using TourismHcm.Application.Services;
using TourismHcm.Domain.Exceptions;
using TourismHcm.UnitTests.TestDoubles;

namespace TourismHcm.UnitTests.Services;

public sealed class MapPackServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tourismhcm-mappack-" + Guid.NewGuid().ToString("N"));

    public MapPackServiceTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "packs", "current"));
        File.WriteAllBytes(Path.Combine(_root, "packs", "current", "hcm.pmtiles"), [1, 2, 3, 4, 5]);
        File.WriteAllText(Path.Combine(_root, "packs", "current", "manifest.json"), """
            {
              "id": "hcm-v1",
              "scope": "hcm",
              "version": "1.0.0",
              "assets": [{ "path": "hcm.pmtiles", "sha256": "HASH_PLACEHOLDER", "bytes": 5 }]
            }
            """);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* temp */ }
    }

    private MapPackService CreateSut() =>
        new(OptionsFactory.Create(new MapPackOptions { DataDir = _root, DefaultScope = "hcm" }));

    [Fact]
    public async Task GetOfflineOptionsAsync_ListsPackFolders()
    {
        var options = await CreateSut().GetOfflineOptionsAsync();
        options.AvailablePacks.Should().Contain("current");
        options.DefaultScope.Should().Be("hcm");
    }

    [Fact]
    public async Task GetOfflineManifestAsync_MergesOnDiskPmtilesAndComputesHash()
    {
        var manifest = await CreateSut().GetOfflineManifestAsync("current");
        manifest.PackId.Should().Be("current");
        manifest.Scope.Should().Be("hcm");
        manifest.Files.Should().ContainSingle(f => f.Path == "hcm.pmtiles");
        var file = manifest.Files.Single();
        file.SizeBytes.Should().Be(5);
        file.Sha256.Should().NotBeNullOrWhiteSpace();
        file.Sha256.Should().NotContain("HASH");
    }

    [Fact]
    public async Task GetOfflineManifestAsync_MissingPack_ReturnsEmptyFiles()
    {
        var manifest = await CreateSut().GetOfflineManifestAsync("missing");
        manifest.Files.Should().BeEmpty();
        manifest.PackId.Should().Be("missing");
    }

    [Fact]
    public async Task GetOfflineManifestAsync_PathTraversal_ThrowsValidation()
    {
        var act = async () => await CreateSut().GetOfflineManifestAsync("../secret");
        await act.Should().ThrowAsync<ValidationDomainException>();
    }
}
