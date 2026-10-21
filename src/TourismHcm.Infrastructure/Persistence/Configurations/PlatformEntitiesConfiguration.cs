using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TourismHcm.Domain.Entities;

namespace TourismHcm.Infrastructure.Persistence.Configurations;

public sealed class OwnerRegistrationConfiguration : IEntityTypeConfiguration<OwnerRegistration>
{
    public void Configure(EntityTypeBuilder<OwnerRegistration> builder)
    {
        builder.ToTable("OwnerRegistrations");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.NationalIdEncrypted).HasMaxLength(500).IsRequired();
        builder.Property(r => r.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Note).HasMaxLength(2000);
        builder.Property(r => r.AdminNote).HasMaxLength(2000);

        builder.HasIndex(r => r.UserId);
        builder.HasIndex(r => r.Status);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PlaceSubmissionConfiguration : IEntityTypeConfiguration<PlaceSubmission>
{
    public void Configure(EntityTypeBuilder<PlaceSubmission> builder)
    {
        builder.ToTable("PlaceSubmissions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.PayloadJson).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.AdminNote).HasMaxLength(2000);

        builder.HasIndex(s => s.UserId);
        builder.HasIndex(s => s.Status);

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Place)
            .WithMany()
            .HasForeignKey(s => s.PlaceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class OwnerNotificationConfiguration : IEntityTypeConfiguration<OwnerNotification>
{
    public void Configure(EntityTypeBuilder<OwnerNotification> builder)
    {
        builder.ToTable("OwnerNotifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Body).HasMaxLength(4000).IsRequired();

        builder.HasIndex(n => n.UserId);
        builder.HasIndex(n => new { n.UserId, n.IsRead });

        builder.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AiUsageLimitConfiguration : IEntityTypeConfiguration<AiUsageLimit>
{
    public void Configure(EntityTypeBuilder<AiUsageLimit> builder)
    {
        builder.ToTable("AiUsageLimits");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.DateUtc).HasColumnType("date");

        builder.HasIndex(l => new { l.UserId, l.DateUtc }).IsUnique();

        builder.HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AnalyticsDeviceConfiguration : IEntityTypeConfiguration<AnalyticsDevice>
{
    public void Configure(EntityTypeBuilder<AnalyticsDevice> builder)
    {
        builder.ToTable("AnalyticsDevices");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.DeviceKeyHash).HasMaxLength(128).IsRequired();

        builder.HasIndex(d => d.DeviceKeyHash).IsUnique();
    }
}

public sealed class AnalyticsSessionConfiguration : IEntityTypeConfiguration<AnalyticsSession>
{
    public void Configure(EntityTypeBuilder<AnalyticsSession> builder)
    {
        builder.ToTable("AnalyticsSessions");

        builder.HasKey(s => s.Id);

        builder.HasIndex(s => s.DeviceId);
        builder.HasIndex(s => s.StartedAt);

        builder.HasOne(s => s.Device)
            .WithMany(d => d.Sessions)
            .HasForeignKey(s => s.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AnalyticsEventConfiguration : IEntityTypeConfiguration<AnalyticsEvent>
{
    public void Configure(EntityTypeBuilder<AnalyticsEvent> builder)
    {
        builder.ToTable("AnalyticsEvents");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.EventName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.PayloadJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(e => e.SessionId);
        builder.HasIndex(e => e.OccurredAt);

        builder.HasOne(e => e.Session)
            .WithMany(s => s.Events)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AnalyticsHourlyMetricConfiguration : IEntityTypeConfiguration<AnalyticsHourlyMetric>
{
    public void Configure(EntityTypeBuilder<AnalyticsHourlyMetric> builder)
    {
        builder.ToTable("AnalyticsHourlyMetrics");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.MetricKey).HasMaxLength(100).IsRequired();

        builder.HasIndex(m => new { m.HourUtc, m.MetricKey }).IsUnique();
    }
}

public sealed class AnalyticsDailyMetricConfiguration : IEntityTypeConfiguration<AnalyticsDailyMetric>
{
    public void Configure(EntityTypeBuilder<AnalyticsDailyMetric> builder)
    {
        builder.ToTable("AnalyticsDailyMetrics");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.MetricKey).HasMaxLength(100).IsRequired();
        builder.Property(m => m.DateUtc).HasColumnType("date");

        builder.HasIndex(m => new { m.DateUtc, m.MetricKey }).IsUnique();
    }
}

public sealed class UiTranslationBundleConfiguration : IEntityTypeConfiguration<UiTranslationBundle>
{
    public void Configure(EntityTypeBuilder<UiTranslationBundle> builder)
    {
        builder.ToTable("UiTranslationBundles");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Namespace).HasMaxLength(200).IsRequired();
        builder.Property(b => b.Locale).HasMaxLength(20).IsRequired();
        builder.Property(b => b.SourceHash).HasMaxLength(64).IsRequired();
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(b => b.PayloadJson).IsRequired().HasColumnType("nvarchar(max)");

        builder.HasIndex(b => new { b.Namespace, b.Locale, b.SourceHash });
    }
}

public sealed class RuntimeLocationHourlyConfiguration : IEntityTypeConfiguration<RuntimeLocationHourly>
{
    public void Configure(EntityTypeBuilder<RuntimeLocationHourly> builder)
    {
        builder.ToTable("RuntimeLocationHourlies");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.BucketKey).HasMaxLength(200).IsRequired();

        builder.HasIndex(r => new { r.HourUtc, r.BucketKey }).IsUnique();
    }
}
