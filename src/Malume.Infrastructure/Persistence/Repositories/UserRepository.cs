using Malume.Application.Abstractions.Persistence;
using Malume.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Malume.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(MalumeDbContext dbContext) : IUserRepository
{
    private readonly MalumeDbContext _dbContext = dbContext;

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        _dbContext.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        _dbContext.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<bool> FleetExistsAsync(Guid fleetId, CancellationToken cancellationToken = default) =>
        _dbContext.Fleets.AnyAsync(f => f.Id == fleetId && f.IsActive, cancellationToken);

    public Task<bool> SchoolExistsAsync(Guid schoolId, CancellationToken cancellationToken = default) =>
        _dbContext.Schools.AnyAsync(s => s.Id == schoolId, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _dbContext.Users.AddAsync(user, cancellationToken);
    }
}
