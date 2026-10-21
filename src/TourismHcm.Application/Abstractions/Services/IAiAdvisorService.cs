using TourismHcm.Contracts.Ai;

namespace TourismHcm.Application.Abstractions.Services;

public interface IAiAdvisorService
{
    Task<AiUsageDto> GetUsageAsync(Guid userId, IReadOnlyList<string> roles, CancellationToken ct = default);

    Task<EnhanceDescriptionResponse> EnhanceDescriptionAsync(
        Guid userId, IReadOnlyList<string> roles, EnhanceDescriptionRequest request, CancellationToken ct = default);
}
