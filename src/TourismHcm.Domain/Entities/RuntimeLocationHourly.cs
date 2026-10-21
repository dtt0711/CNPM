namespace TourismHcm.Domain.Entities;

public class RuntimeLocationHourly
{
    public Guid Id { get; set; }
    public DateTimeOffset HourUtc { get; set; }
    public string BucketKey { get; set; } = string.Empty;
    public long Count { get; set; }
}
