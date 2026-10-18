using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TourismHcm.Domain.Entities;

namespace TourismHcm.Infrastructure.Persistence.Configurations;

public sealed class AudioGenerationTaskConfiguration : IEntityTypeConfiguration<AudioGenerationTask>
{
    public void Configure(EntityTypeBuilder<AudioGenerationTask> builder)
    {
        builder.ToTable("AudioGenerationTasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Lang).HasMaxLength(10).IsRequired();
        builder.Property(t => t.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Error).HasMaxLength(2000);

        // Many historical tasks can exist for the same place+language (one per content
        // revision), so no uniqueness constraint here-only an index for lookup performance.
        builder.HasIndex(t => new { t.PlaceId, t.Lang });
        builder.HasIndex(t => t.Status);
    }
}
