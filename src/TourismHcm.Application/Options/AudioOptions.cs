using TourismHcm.Domain.Constants;

namespace TourismHcm.Application.Options;

public sealed class AudioOptions
{
    public const string SectionName = "Audio";

    public int MaxTextLength { get; set; } = 5000;

    public int MaxRetries { get; set; } = 3;

    /// <summary>Max concurrent TTS generation workers (HTML AudioTaskManager Semaphore max=3).</summary>
    public int MaxParallelTasks { get; set; } = 3;

    public string[] PriorityLanguages { get; set; } = AppLanguages.Priority;
}
