using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TourismHcm.Domain.Entities;

namespace TourismHcm.Infrastructure.Persistence.Configurations;

public sealed class PlaceConfiguration : IEntityTypeConfiguration<Place>
{
    public void Configure(EntityTypeBuilder<Place> builder)
    {
        builder.ToTable("Places");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.DescriptionVi).HasMaxLength(10000).IsRequired();
        builder.Property(p => p.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(p => p.District).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Address).HasMaxLength(300).IsRequired();
        builder.Property(p => p.OpeningHours).HasMaxLength(500);
        builder.Property(p => p.TicketPrice).HasMaxLength(1000);
        builder.Property(p => p.ImageUrl).HasMaxLength(2000);
        builder.Property(p => p.Tags).HasMaxLength(500);
        builder.Property(p => p.Category).HasConversion<string>().HasMaxLength(30);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.AverageRating).HasPrecision(3, 2);

        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.District);
        builder.HasIndex(p => p.OwnerUserId);
        builder.HasIndex(p => new { p.Latitude, p.Longitude });

        builder.HasMany(p => p.Translations)
            .WithOne(t => t.Place)
            .HasForeignKey(t => t.PlaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.AudioAssets)
            .WithOne(a => a.Place)
            .HasForeignKey(a => a.PlaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.AudioTasks)
            .WithOne(t => t.Place)
            .HasForeignKey(t => t.PlaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Reviews)
            .WithOne(r => r.Place)
            .HasForeignKey(r => r.PlaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Favorites)
            .WithOne(f => f.Place)
            .HasForeignKey(f => f.PlaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.TourStops)
            .WithOne(s => s.Place)
            .HasForeignKey(s => s.PlaceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
