using FluentAssertions;
using NSubstitute;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Services;
using TourismHcm.Domain.Constants;

namespace TourismHcm.UnitTests.Services;

public sealed class PermissionServiceTests
{
    private readonly PermissionService _sut = new(Substitute.For<IUserRepository>());

    [Fact]
    public void ResolvePermissions_WhenRoleHasJson_UsesStoredPermissions()
    {
        var perms = _sut.ResolvePermissions(
            [AppRoles.User],
            ["""["place.read","tour.read"]"""]);

        perms.Should().BeEquivalentTo(["place.read", "tour.read"]);
    }

    [Fact]
    public void ResolvePermissions_WhenJsonEmpty_FallsBackToDefaults()
    {
        var perms = _sut.ResolvePermissions([AppRoles.SuperAdmin], ["[]"]);

        perms.Should().Contain(AppPermissions.PlaceApprove);
        perms.Should().HaveCount(AppPermissions.All.Length);
    }

    [Fact]
    public void ParsePermissionsJson_WhenInvalidJson_UsesRoleDefaults()
    {
        var perms = PermissionService.ParsePermissionsJson("{not-json", AppRoles.User).ToList();

        perms.Should().Contain(AppPermissions.PlaceRead);
        perms.Should().Contain(AppPermissions.TourRead);
    }
}
