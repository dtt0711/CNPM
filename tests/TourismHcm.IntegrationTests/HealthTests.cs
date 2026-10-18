using System.Net;
using FluentAssertions;

namespace TourismHcm.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class HealthTests
{
    private readonly TourismHcmWebApplicationFactory _factory;

    public HealthTests(TourismHcmWebApplicationFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task Liveness_ReturnsHealthy()
    {
        var client = await _factory.CreateReadyClientAsync();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Healthy");
    }

    [SkippableFact]
    public async Task Readiness_ReturnsHealthy_WhenSqlAndAudioStorageAreUp()
    {
        var client = await _factory.CreateReadyClientAsync();

        var response = await client.GetAsync("/health/ready");

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        body.Should().Contain("Healthy");
    }
}
