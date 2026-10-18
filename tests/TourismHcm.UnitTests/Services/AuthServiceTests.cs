using FluentAssertions;
using NSubstitute;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Options;
using TourismHcm.Application.Services;
using TourismHcm.Contracts.Auth;
using TourismHcm.Domain.Constants;
using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Exceptions;
using TourismHcm.Domain.Helpers;
using TourismHcm.UnitTests.TestDoubles;

namespace TourismHcm.UnitTests.Services;

public sealed class AuthServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenService _jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly IUnitOfWork _unitOfWork = FakeProviders.CreateUnitOfWork();
    private readonly JwtOptions _jwtOptions = new();

    public AuthServiceTests()
    {
        // Every call to GenerateRefreshToken() returns a fresh unique raw token, mirroring the
        // real crypto-random implementation closely enough to exercise rotation logic.
        _jwtTokenService.GenerateRefreshToken().Returns(_ => Guid.NewGuid().ToString("N"));

        _jwtTokenService
            .CreateTokens(Arg.Any<User>(), Arg.Any<IEnumerable<string>>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>())
            .Returns(call => new AuthResponse
            {
                UserId = call.ArgAt<User>(0).Id,
                Email = call.ArgAt<User>(0).Email,
                DisplayName = call.ArgAt<User>(0).DisplayName,
                Roles = call.ArgAt<IEnumerable<string>>(1).ToArray(),
                AccessToken = "access-token",
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
                RefreshToken = call.ArgAt<string>(2),
                RefreshTokenExpiresAt = call.ArgAt<DateTimeOffset>(3),
            });
    }

    private AuthService CreateSut() => new(
        _userRepository, _passwordHasher, _jwtTokenService, _unitOfWork, OptionsFactory.Create(_jwtOptions));

    // ---------- RegisterAsync ----------

    [Fact]
    public async Task RegisterAsync_WhenEmailAlreadyRegistered_ThrowsConflict()
    {
        _userRepository.EmailExistsAsync("taken@example.com", Arg.Any<CancellationToken>()).Returns(true);

        var sut = CreateSut();
        var request = new RegisterRequest { Email = "taken@example.com", Password = "Password1", DisplayName = "Someone" };

        var act = async () => await sut.RegisterAsync(request);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task RegisterAsync_WhenDefaultRoleNotConfigured_ThrowsDomainException()
    {
        _userRepository.EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _userRepository.GetRoleByNameAsync(AppRoles.User, Arg.Any<CancellationToken>()).Returns((Role?)null);

        var sut = CreateSut();
        var request = new RegisterRequest { Email = "new@example.com", Password = "Password1", DisplayName = "New" };

        var act = async () => await sut.RegisterAsync(request);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task RegisterAsync_WhenValid_HashesPasswordAndPersistsUserWithDefaultRole()
    {
        _userRepository.EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var userRole = EntityFactory.CreateRole(AppRoles.User);
        _userRepository.GetRoleByNameAsync(AppRoles.User, Arg.Any<CancellationToken>()).Returns(userRole);
        _passwordHasher.Hash("Password1").Returns("hashed-value");

        var sut = CreateSut();
        var request = new RegisterRequest { Email = "  New@Example.com  ", Password = "Password1", DisplayName = "New User" };

        var result = await sut.RegisterAsync(request);

        result.Email.Should().Be("new@example.com");
        result.Roles.Should().Contain(AppRoles.User);

        await _userRepository.Received(1).AddAsync(
            Arg.Is<User>(u => u.Email == "new@example.com" && u.PasswordHash == "hashed-value"),
            Arg.Any<CancellationToken>());
        await _userRepository.Received(1).AddRefreshTokenAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- LoginAsync ----------

    [Fact]
    public async Task LoginAsync_WhenUserDoesNotExist_ThrowsUnauthorized()
    {
        _userRepository.GetByEmailWithRolesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var sut = CreateSut();
        var act = async () => await sut.LoginAsync(new LoginRequest { Email = "nobody@example.com", Password = "x" });

        await act.Should().ThrowAsync<UnauthorizedDomainException>();
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordIncorrect_ThrowsUnauthorized()
    {
        var user = EntityFactory.CreateUser();
        _userRepository.GetByEmailWithRolesAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify(Arg.Any<string>(), user.PasswordHash).Returns(false);

        var sut = CreateSut();
        var act = async () => await sut.LoginAsync(new LoginRequest { Email = user.Email, Password = "wrong" });

        await act.Should().ThrowAsync<UnauthorizedDomainException>();
    }

    [Fact]
    public async Task LoginAsync_WhenAccountDisabled_ThrowsForbidden()
    {
        var user = EntityFactory.CreateUser(isActive: false);
        _userRepository.GetByEmailWithRolesAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify(Arg.Any<string>(), user.PasswordHash).Returns(true);

        var sut = CreateSut();
        var act = async () => await sut.LoginAsync(new LoginRequest { Email = user.Email, Password = "correct" });

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task LoginAsync_WhenCredentialsValid_IssuesTokensWithUserRoles()
    {
        var user = EntityFactory.CreateUser();
        var role = EntityFactory.CreateRole(AppRoles.Editor);
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, User = user, Role = role });

        _userRepository.GetByEmailWithRolesAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("correct", user.PasswordHash).Returns(true);

        var sut = CreateSut();
        var result = await sut.LoginAsync(new LoginRequest { Email = user.Email, Password = "correct" });

        result.UserId.Should().Be(user.Id);
        result.Roles.Should().Contain(AppRoles.Editor);
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    // ---------- RefreshAsync ----------

    [Fact]
    public async Task RefreshAsync_WhenTokenDoesNotExist_ThrowsUnauthorized()
    {
        _userRepository.GetRefreshTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((RefreshToken?)null);

        var sut = CreateSut();
        var act = async () => await sut.RefreshAsync(new RefreshRequest { RefreshToken = "bogus" });

        await act.Should().ThrowAsync<UnauthorizedDomainException>();
    }

    [Fact]
    public async Task RefreshAsync_WhenTokenExpiredOrRevoked_ThrowsUnauthorized()
    {
        var userId = Guid.NewGuid();
        var expiredToken = EntityFactory.CreateRefreshToken(userId, "hash", revoked: true);
        _userRepository.GetRefreshTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(expiredToken);

        var sut = CreateSut();
        var act = async () => await sut.RefreshAsync(new RefreshRequest { RefreshToken = "revoked-token" });

        await act.Should().ThrowAsync<UnauthorizedDomainException>();
    }

    [Fact]
    public async Task RefreshAsync_WhenUserNoLongerExists_ThrowsUnauthorized()
    {
        var userId = Guid.NewGuid();
        var activeToken = EntityFactory.CreateRefreshToken(userId, "hash");
        _userRepository.GetRefreshTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(activeToken);
        _userRepository.GetByIdWithRolesAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var sut = CreateSut();
        var act = async () => await sut.RefreshAsync(new RefreshRequest { RefreshToken = "some-token" });

        await act.Should().ThrowAsync<UnauthorizedDomainException>();
    }

    [Fact]
    public async Task RefreshAsync_WhenUserDisabled_ThrowsForbidden()
    {
        var user = EntityFactory.CreateUser(isActive: false);
        var activeToken = EntityFactory.CreateRefreshToken(user.Id, "hash");
        _userRepository.GetRefreshTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(activeToken);
        _userRepository.GetByIdWithRolesAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var sut = CreateSut();
        var act = async () => await sut.RefreshAsync(new RefreshRequest { RefreshToken = "some-token" });

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task RefreshAsync_WhenValid_RotatesToken_RevokingOldOneAndLinkingToNewHash()
    {
        var user = EntityFactory.CreateUser();
        const string rawOldToken = "old-raw-refresh-token";
        var activeToken = EntityFactory.CreateRefreshToken(user.Id, ContentHash.Compute(rawOldToken));

        _userRepository.GetRefreshTokenAsync(ContentHash.Compute(rawOldToken), Arg.Any<CancellationToken>())
            .Returns(activeToken);
        _userRepository.GetByIdWithRolesAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var sut = CreateSut();
        var result = await sut.RefreshAsync(new RefreshRequest { RefreshToken = rawOldToken });

        activeToken.RevokedAt.Should().NotBeNull("the old refresh token must be revoked once rotated");
        activeToken.ReplacedByTokenHash.Should().Be(ContentHash.Compute(result.RefreshToken));
        result.RefreshToken.Should().NotBe(rawOldToken, "a brand new refresh token must be issued on rotation");

        await _userRepository.Received(1).AddRefreshTokenAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    // ---------- LogoutAsync ----------

    [Fact]
    public async Task LogoutAsync_WhenTokenActive_RevokesIt()
    {
        var userId = Guid.NewGuid();
        var token = EntityFactory.CreateRefreshToken(userId, ContentHash.Compute("my-token"));
        _userRepository.GetRefreshTokenAsync(ContentHash.Compute("my-token"), Arg.Any<CancellationToken>())
            .Returns(token);

        var sut = CreateSut();
        await sut.LogoutAsync("my-token");

        token.RevokedAt.Should().NotBeNull();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogoutAsync_WhenTokenDoesNotExist_IsNoOp()
    {
        _userRepository.GetRefreshTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((RefreshToken?)null);

        var sut = CreateSut();
        await sut.LogoutAsync("unknown-token");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogoutAsync_WhenTokenAlreadyRevoked_IsNoOp()
    {
        var userId = Guid.NewGuid();
        var token = EntityFactory.CreateRefreshToken(userId, "hash", revoked: true);
        _userRepository.GetRefreshTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(token);

        var sut = CreateSut();
        await sut.LogoutAsync("already-revoked");

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
