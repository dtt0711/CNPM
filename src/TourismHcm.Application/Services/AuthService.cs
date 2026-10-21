using Microsoft.Extensions.Options;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Application.Options;
using TourismHcm.Contracts.Auth;
using TourismHcm.Domain.Constants;
using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Exceptions;
using TourismHcm.Domain.Helpers;

namespace TourismHcm.Application.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IPermissionService _permissionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtOptions _jwtOptions;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IPermissionService permissionService,
        IUnitOfWork unitOfWork,
        IOptions<JwtOptions> jwtOptions)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _permissionService = permissionService;
        _unitOfWork = unitOfWork;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _userRepository.EmailExistsAsync(email, ct))
        {
            throw new ConflictException($"Email '{email}' is already registered.");
        }

        var role = await _userRepository.GetRoleByNameAsync(AppRoles.User, ct)
            ?? throw new DomainException("role_missing", $"Default role '{AppRoles.User}' is not configured.", 500);

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            IsActive = true,
            CreatedAt = now
        };

        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, Role = role });

        await _userRepository.AddAsync(user, ct);

        var response = await IssueTokensAsync(user, user, [AppRoles.User], ct);

        await _unitOfWork.SaveChangesAsync(ct);

        return response;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailWithRolesAsync(email, ct);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedDomainException("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            throw new ForbiddenException("This account has been disabled.");
        }

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToArray();
        var response = await IssueTokensAsync(user, user, roles, ct);

        await _unitOfWork.SaveChangesAsync(ct);

        return response;
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var tokenHash = ContentHash.Compute(request.RefreshToken);
        var existingToken = await _userRepository.GetRefreshTokenAsync(tokenHash, ct);

        if (existingToken is null || !existingToken.IsActive)
        {
            throw new UnauthorizedDomainException("Invalid or expired refresh token.");
        }

        var user = await _userRepository.GetByIdWithRolesAsync(existingToken.UserId, ct)
            ?? throw new UnauthorizedDomainException("Invalid or expired refresh token.");

        if (!user.IsActive)
        {
            throw new ForbiddenException("This account has been disabled.");
        }

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToArray();
        var response = await IssueTokensAsync(user, user, roles, ct);

        var now = DateTimeOffset.UtcNow;
        existingToken.RevokedAt = now;
        existingToken.ReplacedByTokenHash = ContentHash.Compute(response.RefreshToken);

        await _unitOfWork.SaveChangesAsync(ct);

        return response;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var tokenHash = ContentHash.Compute(refreshToken);
        var existingToken = await _userRepository.GetRefreshTokenAsync(tokenHash, ct);

        if (existingToken is null || existingToken.RevokedAt is not null)
        {
            return;
        }

        existingToken.RevokedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<AuthResponse> IssueTokensAsync(
        User user, User userWithRoles, IEnumerable<string> roles, CancellationToken ct)
    {
        var rawRefreshToken = _jwtTokenService.GenerateRefreshToken();
        var refreshExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenDays);

        await _userRepository.AddRefreshTokenAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = ContentHash.Compute(rawRefreshToken),
            ExpiresAt = refreshExpiresAt,
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);

        var roleList = roles.ToList();
        var permissionsJson = userWithRoles.UserRoles.Select(ur => ur.Role.PermissionsJson);
        var permissions = _permissionService.ResolvePermissions(roleList, permissionsJson);

        return _jwtTokenService.CreateTokens(user, roleList, permissions, rawRefreshToken, refreshExpiresAt);
    }
}
