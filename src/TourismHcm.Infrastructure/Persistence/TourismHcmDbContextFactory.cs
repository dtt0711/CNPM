using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TourismHcm.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core CLI when the Api startup project does not build.
/// </summary>
public sealed class TourismHcmDbContextFactory : IDesignTimeDbContextFactory<TourismHcmDbContext>
{
    public TourismHcmDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TourismHcmDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=TourismHcmDesign;Trusted_Connection=True;TrustServerCertificate=True");
        return new TourismHcmDbContext(optionsBuilder.Options);
    }
}
