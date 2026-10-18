using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TourismHcm.Domain.Entities;

namespace TourismHcm.Infrastructure.Persistence.Configurations;

public sealed class PlaceTranslationConfiguration : IEntityTypeConfiguration<PlaceTranslation>
{
    public void Configure(EntityTypeBuilder<PlaceTranslation> builder)
    {
        builder.ToTable("PlaceTranslations");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Lang).HasMaxLength(10).IsRequired();
        builder.Property(t => t.Text).HasMaxLength(10000).IsRequired();
        builder.Property(t => t.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(t => t.Source).HasConversion<string>().HasMaxLength(20);

        // Only one translation per place+language pair.
        builder.HasIndex(t => new { t.PlaceId, t.Lang }).IsUnique();
    }
}
