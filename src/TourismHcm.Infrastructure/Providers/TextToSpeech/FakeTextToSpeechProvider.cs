using TourismHcm.Application.Abstractions.Providers;

namespace TourismHcm.Infrastructure.Providers.TextToSpeech;

/// <summary>
/// Silent MP3 stub registered only in the Testing environment (unit/integration hosts).
/// </summary>
public sealed class FakeTextToSpeechProvider : ITextToSpeechProvider
{
    // A single MPEG-1 Layer III, 128kbps, 44.1kHz, mono frame header (no CRC) followed by
    // zeroed payload bytes. Repeating this frame yields a tiny, silent, structurally valid MP3.
    private const int FrameSizeBytes = 417;
    private static readonly byte[] FrameHeader = [0xFF, 0xFB, 0x90, 0x44];
    private const int FramesPerSecond = 38; // ~26ms per frame at this bitrate/sample rate.

    public string ProviderName => "Fake";

    public bool IsConfigured => true;

    public Task<byte[]> SynthesizeAsync(string text, string lang, CancellationToken ct = default)
    {
        var wordCount = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var estimatedSeconds = Math.Clamp(wordCount / 2.5, 1, 120);
        var frameCount = Math.Max(1, (int)(estimatedSeconds * FramesPerSecond));

        var buffer = new byte[frameCount * FrameSizeBytes];
        for (var i = 0; i < frameCount; i++)
        {
            FrameHeader.CopyTo(buffer, i * FrameSizeBytes);
        }

        return Task.FromResult(buffer);
    }
}
