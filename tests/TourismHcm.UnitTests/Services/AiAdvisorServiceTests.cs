using FluentAssertions;
using NSubstitute;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Options;
using TourismHcm.Application.Services;
using TourismHcm.Contracts.Ai;
using TourismHcm.Domain.Constants;
using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Exceptions;
using TourismHcm.UnitTests.TestDoubles;

namespace TourismHcm.UnitTests.Services;

public sealed class AiAdvisorServiceTests
{
    private readonly IPlatformRepository _platform = Substitute.For<IPlatformRepository>();
    private readonly IAiAdvisorProvider _provider = Substitute.For<IAiAdvisorProvider>();
    private readonly IUnitOfWork _uow = FakeProviders.CreateUnitOfWork();
    private readonly AiOptions _options = new() { OwnerDailyLimit = 2 };

    private AiAdvisorService CreateSut() =>
        new(_platform, _provider, _uow, OptionsFactory.Create(_options));

    [Fact]
    public async Task GetUsageAsync_Admin_IsUnlimited()
    {
        var sut = CreateSut();
        var result = await sut.GetUsageAsync(Guid.NewGuid(), [AppRoles.Admin]);

        result.IsUnlimited.Should().BeTrue();
        result.DailyLimit.Should().BeNull();
        await _platform.DidNotReceive().GetAiUsageForDateAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnhanceDescriptionAsync_WhenQuotaExceeded_Throws()
    {
        var userId = Guid.NewGuid();
        _platform.GetAiUsageForDateAsync(userId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new AiUsageLimit { UserId = userId, Count = 2 });

        var sut = CreateSut();
        var act = async () => await sut.EnhanceDescriptionAsync(
            userId, [AppRoles.User], new EnhanceDescriptionRequest { SourceText = "hello" });

        await act.Should().ThrowAsync<DomainException>().Where(e => e.Code == "ai_quota_exceeded");
    }

    [Fact]
    public async Task EnhanceDescriptionAsync_IncrementsUsage_ForNonAdmin()
    {
        var userId = Guid.NewGuid();
        _provider.IsConfigured.Returns(true);
        _provider.ProviderName.Returns("Gemini");
        _provider.EnhanceDescriptionAsync("hello", "vi", Arg.Any<CancellationToken>()).Returns("xin chao");
        _platform.GetAiUsageForDateAsync(userId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((AiUsageLimit?)null);

        var sut = CreateSut();
        var result = await sut.EnhanceDescriptionAsync(
            userId, [AppRoles.User], new EnhanceDescriptionRequest { SourceText = "hello", Lang = "vi" });

        result.EnhancedText.Should().Be("xin chao");
        result.UsedToday.Should().Be(1);
        await _platform.Received(1).IncrementAiUsageAsync(userId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnhanceDescriptionAsync_EmptyText_ThrowsValidation()
    {
        var sut = CreateSut();
        var act = async () => await sut.EnhanceDescriptionAsync(
            Guid.NewGuid(), [AppRoles.Admin], new EnhanceDescriptionRequest { SourceText = "  " });

        await act.Should().ThrowAsync<ValidationDomainException>();
    }
}
