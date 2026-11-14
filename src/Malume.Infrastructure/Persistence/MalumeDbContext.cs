using Malume.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Malume.Infrastructure.Persistence;

public sealed class MalumeDbContext(DbContextOptions<MalumeDbContext> options)
    : DbContext(options), IMalumeDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MalumeDbContext).Assembly);
    }
}
