using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TourismHcm.Contracts.Auth;
using TourismHcm.Domain.Constants;

namespace TourismHcm.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class AuthRbacTests
{
    private readonly TourismHcmWebApplicationFactory _factory;

    public AuthRbacTests(TourismHcmWebApplicationFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task RegisterLoginAndRefresh_IssueTokens_AndOldRefreshTokenIsRejected()
    {
        var client = await _factory.CreateReadyClientAsync();
        var email = $"user-{Guid.NewGuid():N}@example.com";

        var registered = await Api.RegisterAsync(client, email);
        registered.Email.Should().Be(email);
        registered.Roles.Should().Contain(AppRoles.User);
        registered.AccessToken.Should().NotBeNullOrWhiteSpace();
        registered.RefreshToken.Should().NotBeNullOrWhiteSpace();

        var loggedIn = await Api.LoginAsync(client, email, "Password123");
        loggedIn.UserId.Should().Be(registered.UserId);
        loggedIn.Roles.Should().Contain(AppRoles.User);

        var refreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest
        {
            RefreshToken = loggedIn.RefreshToken,
        });
        var refreshed = await Api.ReadAsync<AuthResponse>(refreshResponse, HttpStatusCode.OK);
        refreshed.AccessToken.Should().NotBeNullOrWhiteSpace();
        refreshed.RefreshToken.Should().NotBe(loggedIn.RefreshToken);

        var replay = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest
        {
            RefreshToken = loggedIn.RefreshToken,
        });
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task AnonymousAndUser_CannotMutatePlaces_EditorCanUpdate_OnlyAdminCanDelete()
    {
        var admin = await _factory.CreateAdminClientAsync();
        var placeResponse = await admin.PostAsJsonAsync("/api/v1/places", Api.SamplePlace());
        var placeId = (await Api.ReadAsync<TourismHcm.Contracts.Places.PlaceDetailDto>(placeResponse, HttpStatusCode.Created)).Id;

        var anonymous = await _factory.CreateReadyClientAsync();
        var anonymousCreate = await anonymous.PostAsJsonAsync("/api/v1/places", Api.SamplePlace());
        anonymousCreate.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var email = $"member-{Guid.NewGuid():N}@example.com";
        var userClient = await _factory.CreateReadyClientAsync();
        var registered = await Api.RegisterAsync(userClient, email);
        userClient.UseBearer(registered.AccessToken);

        var userCreate = await userClient.PostAsJsonAsync("/api/v1/places", Api.SamplePlace());
        userCreate.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var userDelete = await userClient.DeleteAsync($"/api/v1/places/{placeId}");
        userDelete.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await _factory.GrantRoleAsync(email, AppRoles.EditorLegacyName);
        var editorLogin = await Api.LoginAsync(userClient, email, "Password123");
        editorLogin.Roles.Should().Contain(AppRoles.EditorLegacyName);
        userClient.UseBearer(editorLogin.AccessToken);

        var editorUpdate = await userClient.PutAsJsonAsync($"/api/v1/places/{placeId}", new TourismHcm.Contracts.Places.UpdatePlaceRequest
        {
            Name = "Editor renamed place",
        });
        editorUpdate.StatusCode.Should().Be(HttpStatusCode.OK);

        var editorDelete = await userClient.DeleteAsync($"/api/v1/places/{placeId}");
        editorDelete.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var adminDelete = await admin.DeleteAsync($"/api/v1/places/{placeId}");
        adminDelete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
