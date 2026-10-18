using Microsoft.EntityFrameworkCore;
using TourismHcm.Domain.Entities;

namespace TourismHcm.Infrastructure.Persistence;

/// <summary>
/// Entity Framework Core database context for the TourismHcm domain model.
/// </summary>
public sealed class TourismHcmDbContext : DbContext
{
    public TourismHcmDbContext(DbContextOptions<TourismHcmDbContext> options)
        : base(options)
    {
    }

    public DbSet<Place> Places => Set<Place>();

    public DbSet<PlaceTranslation> PlaceTranslations => Set<PlaceTranslation>();

    public DbSet<AudioAsset> AudioAssets => Set<AudioAsset>();

    public DbSet<AudioGenerationTask> AudioGenerationTasks => Set<AudioGenerationTask>();

    public DbSet<Review> Reviews => Set<Review>();

    public DbSet<Favorite> Favorites => Set<Favorite>();

    public DbSet<Tour> Tours => Set<Tour>();

    public DbSet<TourStop> TourStops => Set<TourStop>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TourismHcmDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
