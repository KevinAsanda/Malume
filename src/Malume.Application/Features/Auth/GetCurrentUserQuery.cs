using Malume.Application.Abstractions.Identity;
using Malume.Application.Abstractions.Persistence;
using Malume.Application.Common.Exceptions;
using Malume.Domain.Entities;
using Malume.Shared.Contracts.Auth;
using MediatR;

namespace Malume.Application.Features.Auth;

public sealed record GetCurrentUserQuery : IRequest<UserClaimsResponse>;

public sealed class GetCurrentUserQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository)
    : IRequestHandler<GetCurrentUserQuery, UserClaimsResponse>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IUserRepository _userRepository = userRepository;

    public async Task<UserClaimsResponse> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        if (_currentUserService.UserId is not Guid userId)
        {
            throw new UnauthorizedException("User identity claim is missing.");
        }

        User? user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedException("Authenticated user account was not found or is inactive.");
        }

        return new UserClaimsResponse(
            user.Id,
            user.Email,
            user.FullName,
            user.Role.ToString(),
            user.TenantFleetId,
            user.TenantSchoolId);
    }
}
