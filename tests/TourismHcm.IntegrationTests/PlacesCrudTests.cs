using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TourismHcm.Contracts.Common;
using TourismHcm.Contracts.Places;

namespace TourismHcm.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class PlacesCrudTests
{
    private readonly TourismHcmWebApplicationFactory _factory;

    public PlacesCrudTests(TourismHcmWebApplicationFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task Admin_CanCreateReadUpdateAndDelete_APlace()
    {
        var client = await _factory.CreateAdminClientAsync();
        var createdName = $"CRUD {Guid.NewGuid():N}";
        var create = Api.SamplePlace(createdName);

        var createResponse = await client.PostAsJsonAsync("/api/v1/places", create);
        var created = await Api.ReadAsync<PlaceDetailDto>(createResponse, HttpStatusCode.Created);
        created.Id.Should().NotBeEmpty();
        created.Name.Should().Be(createdName);
        created.Status.Should().Be("Draft");
        created.Description.Should().Be(create.DescriptionVi);

        var getResponse = await client.GetAsync($"/api/v1/places/{created.Id}");
        var fetched = await Api.ReadAsync<PlaceDetailDto>(getResponse, HttpStatusCode.OK);
        fetched.Name.Should().Be(createdName);
        fetched.Ward.Should().Be(create.Ward);

        var updatedName = createdName + " updated";
        var updateResponse = await client.PutAsJsonAsync($"/api/v1/places/{created.Id}", new UpdatePlaceRequest
        {
            Name = updatedName,
            Status = "Published",
            DescriptionVi = create.DescriptionVi + " Đã cập nhật.",
        });
        var updated = await Api.ReadAsync<PlaceDetailDto>(updateResponse, HttpStatusCode.OK);
        updated.Name.Should().Be(updatedName);
        updated.Status.Should().Be("Published");

        var searchResponse = await client.GetAsync($"/api/v1/places?search={Uri.EscapeDataString(updatedName)}");
        var page = await Api.ReadAsync<PagedResult<PlaceDto>>(searchResponse, HttpStatusCode.OK);
        page.Items.Should().Contain(p => p.Id == created.Id && p.Name == updatedName);

        var deleteResponse = await client.DeleteAsync($"/api/v1/places/{created.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var missing = await client.GetAsync($"/api/v1/places/{created.Id}");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
