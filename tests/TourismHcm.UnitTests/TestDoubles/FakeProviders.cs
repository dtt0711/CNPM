using NSubstitute;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Abstractions.Repositories;

namespace TourismHcm.UnitTests.TestDoubles;

/// <summary>
/// Factory methods building pre-configured NSubstitute doubles for the external provider
/// abstractions (translation / text-to-speech). Centralised here so every service test that
/// needs a "fake, but configured" or "fake, but unavailable" provider gets identical behaviour.
/// </summary>
public static class FakeProviders
{
    public static ITranslationProvider CreateTranslationProvider(bool isConfigured = true, string? translatedText = null)
    {
        var provider = Substitute.For<ITranslationProvider>();
        provider.IsConfigured.Returns(isConfigured);
        provider.ProviderName.Returns("FakeTranslation");

        if (isConfigured)
        {
            provider
                .TranslateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(call => Task.FromResult(translatedText ?? $"[{call.ArgAt<string>(2)}] {call.ArgAt<string>(0)}"));
        }

        return provider;
    }

    public static ITextToSpeechProvider CreateTextToSpeechProvider(bool isConfigured = true, byte[]? audioBytes = null)
    {
        var provider = Substitute.For<ITextToSpeechProvider>();
        provider.IsConfigured.Returns(isConfigured);
        provider.ProviderName.Returns("FakeTts");

        if (isConfigured)
        {
            provider
                .SynthesizeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(audioBytes ?? new byte[] { 1, 2, 3, 4 }));
        }

        return provider;
    }

    public static IAudioStorage CreateAudioStorage()
    {
        var storage = Substitute.For<IAudioStorage>();
        storage
            .SaveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult($"{call.ArgAt<Guid>(0)}/{call.ArgAt<string>(1)}.mp3"));
        storage
            .GetPublicUrl(Arg.Any<string>())
            .Returns(call => $"/audio/{call.Arg<string>()}");
        return storage;
    }

    public static IAudioTaskQueue CreateAudioTaskQueue() => Substitute.For<IAudioTaskQueue>();

    public static IUnitOfWork CreateUnitOfWork()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(1));
        return unitOfWork;
    }
}
