using Microsoft.Extensions.Options;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Application.Options;
using TourismHcm.Contracts.Ai;
using TourismHcm.Domain.Constants;
using TourismHcm.Domain.Exceptions;

namespace TourismHcm.Application.Services;

public sealed class AiAdvisorService : IAiAdvisorService
{
    private readonly IPlatformRepository _platformRepository;
    private readonly IAiAdvisorProvider _aiAdvisorProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AiOptions _options;

    public AiAdvisorService(
        IPlatformRepository platformRepository,
        IAiAdvisorProvider aiAdvisorProvider,
        IUnitOfWork unitOfWork,
        IOptions<AiOptions> options)
    {
        _platformRepository = platformRepository;
        _aiAdvisorProvider = aiAdvisorProvider;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    public async Task<AiUsageDto> GetUsageAsync(Guid userId, IReadOnlyList<string> roles, CancellationToken ct = default)
    {
        if (IsUnlimited(roles))
        {
            return new AiUsageDto { UsedToday = 0, DailyLimit = null, IsUnlimited = true };
        }

        var today = DateTime.UtcNow.Date;
        var usage = await _platformRepository.GetAiUsageForDateAsync(userId, today, ct);
        return new AiUsageDto
        {
            UsedToday = usage?.Count ?? 0,
            DailyLimit = _options.OwnerDailyLimit,
            IsUnlimited = false
        };
    }

    public async Task<EnhanceDescriptionResponse> EnhanceDescriptionAsync(
        Guid userId, IReadOnlyList<string> roles, EnhanceDescriptionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.SourceText))
        {
            throw new ValidationDomainException(new Dictionary<string, string[]>
            {
                [nameof(request.SourceText)] = ["Source text is required."]
            });
        }

        var unlimited = IsUnlimited(roles);
        var today = DateTime.UtcNow.Date;
        var used = 0;

        if (!unlimited)
        {
            var usage = await _platformRepository.GetAiUsageForDateAsync(userId, today, ct);
            used = usage?.Count ?? 0;
            if (used >= _options.OwnerDailyLimit)
            {
                throw new DomainException("ai_quota_exceeded", "Daily AI usage limit reached.", 429);
            }
        }

        if (!_aiAdvisorProvider.IsConfigured)
        {
            throw new ProviderUnavailableException(_aiAdvisorProvider.ProviderName, "AI advisor is not configured.");
        }

        var enhanced = await _aiAdvisorProvider.EnhanceDescriptionAsync(
            request.SourceText.Trim(), request.Lang, ct);

        if (!unlimited)
        {
            await _platformRepository.IncrementAiUsageAsync(userId, today, ct);
            used += 1;
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return new EnhanceDescriptionResponse
        {
            EnhancedText = enhanced,
            UsedToday = used,
            DailyLimit = unlimited ? null : _options.OwnerDailyLimit
        };
    }

    private static bool IsUnlimited(IReadOnlyList<string> roles) =>
        roles.Any(r => string.Equals(r, AppRoles.Admin, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(r, AppRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase));
}
