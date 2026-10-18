using FluentAssertions;
using TourismHcm.Application.Options;
using TourismHcm.Application.Services;
using TourismHcm.Domain.Constants;
using TourismHcm.UnitTests.TestDoubles;

namespace TourismHcm.UnitTests.Services;

public sealed class LanguageServiceTests
{
    [Fact]
    public void GetLanguages_ReturnsAllSupportedLanguages_WithVoiceIdsAndPriorityFlags()
    {
        var options = new SpeechOptions
        {
            Voices = new Dictionary<string, string>
            {
                [AppLanguages.Vietnamese] = "vi-VN-Voice",
                [AppLanguages.English] = "en-US-Voice",
            },
        };
        var sut = new LanguageService(OptionsFactory.Create(options));

        var languages = sut.GetLanguages();

        languages.Should().HaveCount(AppLanguages.Supported.Length);
        languages.Should().ContainSingle(l => l.Code == AppLanguages.Vietnamese && l.VoiceId == "vi-VN-Voice");
        languages.Should().ContainSingle(l => l.Code == AppLanguages.English && l.VoiceId == "en-US-Voice");
        languages.Where(l => AppLanguages.Priority.Contains(l.Code)).Should().OnlyContain(l => l.IsPriority);
    }

    [Fact]
    public void GetLanguages_WhenVoiceNotConfigured_ReturnsNullVoiceId()
    {
        var options = new SpeechOptions { Voices = new Dictionary<string, string>() };
        var sut = new LanguageService(OptionsFactory.Create(options));

        var languages = sut.GetLanguages();

        languages.Should().OnlyContain(l => l.VoiceId == null);
    }
}
