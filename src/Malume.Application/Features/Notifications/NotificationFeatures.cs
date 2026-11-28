using FluentValidation;
using Malume.Application.Abstractions.Identity;
using Malume.Application.Abstractions.Persistence;
using Malume.Application.Common;
using Malume.Domain.Entities;
using Malume.Shared.Contracts.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Malume.Application.Features.Notifications;

public sealed record RegisterDeviceTokenCommand(
    string Token,
    string Platform,
    string AppVariant) : IRequest<DeviceTokenRegistrationDto>;

public sealed class RegisterDeviceTokenCommandValidator : AbstractValidator<RegisterDeviceTokenCommand>
{
    public RegisterDeviceTokenCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(512);
        RuleFor(x => x.Platform).NotEmpty().MaximumLength(32);
        RuleFor(x => x.AppVariant).NotEmpty().MaximumLength(32);
    }
}

public sealed class RegisterDeviceTokenCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext,
    TimeProvider timeProvider) : IRequestHandler<RegisterDeviceTokenCommand, DeviceTokenRegistrationDto>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<DeviceTokenRegistrationDto> Handle(
        RegisterDeviceTokenCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        string normalizedToken = request.Token.Trim();
        DateTimeOffset nowUtc = _timeProvider.GetUtcNow();

        UserDeviceToken? existing = await _dbContext.UserDeviceTokens
            .FirstOrDefaultAsync(t => t.UserId == user.Id && t.Token == normalizedToken, cancellationToken);

        if (existing is null)
        {
            existing = new UserDeviceToken(user.Id, normalizedToken, request.Platform, request.AppVariant, nowUtc);
            _dbContext.UserDeviceTokens.Add(existing);
        }
        else
        {
            existing.RefreshSeen(request.Platform, request.AppVariant, nowUtc);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DeviceTokenRegistrationDto(
            existing.Id,
            existing.UserId,
            existing.Platform,
            existing.AppVariant,
            existing.LastSeenAtUtc);
    }
}

public sealed record UnregisterDeviceTokenCommand(string Token) : IRequest<Unit>;

public sealed class UnregisterDeviceTokenCommandValidator : AbstractValidator<UnregisterDeviceTokenCommand>
{
    public UnregisterDeviceTokenCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(512);
    }
}

public sealed class UnregisterDeviceTokenCommandHandler(
    ICurrentUserService currentUserService,
    IMalumeDbContext dbContext) : IRequestHandler<UnregisterDeviceTokenCommand, Unit>
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IMalumeDbContext _dbContext = dbContext;

    public async Task<Unit> Handle(UnregisterDeviceTokenCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        User user = await CurrentUserGuard.RequireCurrentUserAsync(_currentUserService, _dbContext, cancellationToken);
        string normalizedToken = request.Token.Trim();

        UserDeviceToken? existing = await _dbContext.UserDeviceTokens
            .FirstOrDefaultAsync(t => t.UserId == user.Id && t.Token == normalizedToken, cancellationToken);

        if (existing is not null)
        {
            _dbContext.UserDeviceTokens.Remove(existing);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Unit.Value;
    }
}
