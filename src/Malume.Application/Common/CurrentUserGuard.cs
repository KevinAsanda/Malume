using Malume.Application.Abstractions.Identity;
using Malume.Application.Abstractions.Persistence;
using Malume.Application.Common.Exceptions;
using Malume.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Malume.Application.Common;

public static class CurrentUserGuard
{
    public static async Task<User> RequireCurrentUserAsync(
        ICurrentUserService currentUser,
        IMalumeDbContext dbContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(dbContext);

        if (currentUser.UserId is not Guid userId)
        {
            throw new UnauthorizedException("User identity claim is missing.");
        }

        User? user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedException("Authenticated user account was not found or is inactive.");
        }

        return user;
    }
}
