using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TourismHcm.Domain.Entities;

namespace TourismHcm.Infrastructure.Persistence.Configurations;

public sealed class TourConfiguration : IEntityTypeConfiguration<Tour>
{
    public void Configure(EntityTypeBuilder<Tour> builder)
    {
        builder.ToTable("Tours");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();

        builder.HasMany(t => t.Stops)
            .WithOne(s => s.Tour)
            .HasForeignKey(s => s.TourId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TourStopConfiguration : IEntityTypeConfiguration<TourStop>
{
    public void Configure(EntityTypeBuilder<TourStop> builder)
    {
        builder.ToTable("TourStops");

        builder.HasKey(s => s.Id);

        builder.HasIndex(s => new { s.TourId, s.Order });
    }
}
