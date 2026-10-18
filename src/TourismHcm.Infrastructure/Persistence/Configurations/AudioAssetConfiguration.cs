using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TourismHcm.Domain.Entities;

namespace TourismHcm.Infrastructure.Persistence.Configurations;

public sealed class AudioAssetConfiguration : IEntityTypeConfiguration<AudioAsset>
{
    public void Configure(EntityTypeBuilder<AudioAsset> builder)
    {
        builder.ToTable("AudioAssets");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Lang).HasMaxLength(10).IsRequired();
        builder.Property(a => a.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Path).HasMaxLength(500).IsRequired();
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);

        // IAudioAssetRepository.GetAsync(placeId, lang) returns a single current asset per
        // place+language pair, so this combination must be unique.
        builder.HasIndex(a => new { a.PlaceId, a.Lang }).IsUnique();
        builder.HasIndex(a => a.Status);
    }
}
