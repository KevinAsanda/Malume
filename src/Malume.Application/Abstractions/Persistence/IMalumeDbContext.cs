namespace Malume.Application.Abstractions.Persistence;

public interface IMalumeDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
