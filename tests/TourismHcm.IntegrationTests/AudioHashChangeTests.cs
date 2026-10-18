using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TourismHcm.Api.Controllers.v1;
using TourismHcm.Contracts.Audio;
using TourismHcm.Contracts.Places;
using TourismHcm.Domain.Helpers;

namespace TourismHcm.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class AudioHashChangeTests
{
    private readonly TourismHcmWebApplicationFactory _factory;

    public AudioHashChangeTests(TourismHcmWebApplicationFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task ChangingDescription_UpdatesContentHash_AndEnqueuesAudioForTheNewText()
    {
        var client = await _factory.CreateAdminClientAsync();
        const string original = "Nội dung gốc dùng để sinh audio lần đầu cho bài kiểm thử hash.";
        const string revised = "Nội dung đã đổi, hash và tác vụ audio phải được tạo lại theo văn bản mới.";

        var createResponse = await client.PostAsJsonAsync("/api/v1/places", Api.SamplePlace(descriptionVi: original));
        var place = await Api.ReadAsync<PlaceDetailDto>(createResponse, HttpStatusCode.Created);

        var ttsResponse = await client.PostAsJsonAsync("/api/v1/audio/tts", new TtsRequest
        {
            PlaceId = place.Id,
            Lang = "vi",
        });
        var firstTask = await Api.ReadAsync<AudioTaskDto>(ttsResponse, HttpStatusCode.OK);
        firstTask.Lang.Should().Be("vi");
        firstTask.PlaceId.Should().Be(place.Id);

        var originalHash = ContentHash.Compute(original);
        var hashesBefore = await _factory.GetAudioContentHashesAsync(place.Id, "vi");
        hashesBefore.Should().Contain(originalHash);

        var storedBefore = await _factory.RequirePlaceAsync(place.Id);
        storedBefore.ContentHash.Should().Be(originalHash);

        var updateResponse = await client.PutAsJsonAsync($"/api/v1/places/{place.Id}", new UpdatePlaceRequest
        {
            DescriptionVi = revised,
        });
        var updated = await Api.ReadAsync<PlaceDetailDto>(updateResponse, HttpStatusCode.OK);
        updated.Description.Should().Be(revised);

        var revisedHash = ContentHash.Compute(revised);
        revisedHash.Should().NotBe(originalHash);

        var storedAfter = await _factory.RequirePlaceAsync(place.Id);
        storedAfter.ContentHash.Should().Be(revisedHash);
        storedAfter.DescriptionVi.Should().Be(revised);

        var hashesAfter = await _factory.GetAudioContentHashesAsync(place.Id, "vi");
        hashesAfter.Should().Contain(revisedHash);
        hashesAfter.Should().Contain(originalHash);
    }
}
