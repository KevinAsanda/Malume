using FluentValidation;
using Malume.Application.Abstractions.Identity;
using Malume.Application.Abstractions.Persistence;
using Malume.Application.Common.Exceptions;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Shared.Contracts.Auth;
using MediatR;

namespace Malume.Application.Features.Auth;

public sealed record RegisterUserCommand(
    string Email,
    string FullName,
    string PhoneNumber,
    string Password,
    string Role,
    Guid? TenantFleetId = null,
    Guid? TenantSchoolId = null) : IRequest<AuthResponse>;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(256)
            .EmailAddress();

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .MinimumLength(7)
            .MaximumLength(25);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128);

        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(BeAllowedSelfRegistrationRole)
            .WithMessage("Role must be one of: Parent, Driver, FleetOwner, or SchoolAdmin.");
    }

    private static bool BeAllowedSelfRegistrationRole(string role) =>
        Enum.TryParse<UserRole>(role, ignoreCase: true, out UserRole parsed)
        && parsed != UserRole.SysAdmin;
}

public sealed class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IMalumeDbContext dbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator)
    : IRequestHandler<RegisterUserCommand, AuthResponse>
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IMalumeDbContext _dbContext = dbContext;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator = jwtTokenGenerator;

    public async Task<AuthResponse> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string normalizedEmail = request.Email.Trim().ToUpperInvariant();
        if (await _userRepository.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            throw new ConflictException($"An account with email '{request.Email.Trim()}' already exists.");
        }

        if (request.TenantFleetId is Guid fleetId && !await _userRepository.FleetExistsAsync(fleetId, cancellationToken))
        {
            throw new NotFoundException($"Fleet tenant '{fleetId}' was not found.");
        }

        if (request.TenantSchoolId is Guid schoolId && !await _userRepository.SchoolExistsAsync(schoolId, cancellationToken))
        {
            throw new NotFoundException($"School tenant '{schoolId}' was not found.");
        }

        UserRole parsedRole = Enum.Parse<UserRole>(request.Role, ignoreCase: true);
        string passwordHash = _passwordHasher.HashPassword(request.Password);

        var user = new User(
            request.Email,
            request.FullName,
            request.PhoneNumber,
            passwordHash,
            parsedRole,
            request.TenantFleetId,
            request.TenantSchoolId);

        await _userRepository.AddAsync(user, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        GeneratedJwtToken token = _jwtTokenGenerator.GenerateToken(user);

        return new AuthResponse(
            token.AccessToken,
            token.ExpiresAtUtc,
            user.Id,
            user.Email,
            user.FullName,
            user.Role.ToString(),
            user.TenantFleetId,
            user.TenantSchoolId);
    }
}
