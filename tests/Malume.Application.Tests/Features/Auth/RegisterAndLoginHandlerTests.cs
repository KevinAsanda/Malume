using Malume.Application.Abstractions.Identity;
using Malume.Application.Abstractions.Persistence;
using Malume.Application.Common.Exceptions;
using Malume.Application.Features.Auth;
using Malume.Domain.Entities;
using Malume.Domain.Enums;
using Malume.Shared.Contracts.Auth;
using NSubstitute;
using Shouldly;

namespace Malume.Application.Tests.Features.Auth;

public sealed class RegisterAndLoginHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IMalumeDbContext _dbContext = Substitute.For<IMalumeDbContext>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenGenerator _jwtTokenGenerator = Substitute.For<IJwtTokenGenerator>();

    [Fact]
    public void RegisterValidator_ShouldRejectSysAdminSelfRegistration()
    {
        var validator = new RegisterUserCommandValidator();
        var command = new RegisterUserCommand(
            "admin@malume.co.za",
            "Admin User",
            "+27821234567",
            "StrongPass!123",
            MalumeRoles.SysAdmin);

        FluentValidation.Results.ValidationResult result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task RegisterHandler_WhenEmailExists_ShouldThrowConflictException()
    {
        _userRepository.EmailExistsAsync("PARENT@MALUME.CO.ZA", Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new RegisterUserCommandHandler(
            _userRepository,
            _dbContext,
            _passwordHasher,
            _jwtTokenGenerator);

        var command = new RegisterUserCommand(
            "parent@malume.co.za",
            "Thandi Mokoena",
            "+27821234567",
            "StrongPass!123",
            MalumeRoles.Parent);

        await Should.ThrowAsync<ConflictException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterHandler_WhenValid_ShouldHashPasswordSaveAndIssueJwt()
    {
        _userRepository.EmailExistsAsync("DRIVER@MALUME.CO.ZA", Arg.Any<CancellationToken>())
            .Returns(false);
        _passwordHasher.HashPassword("StrongPass!123").Returns("hashed_pw");
        DateTimeOffset expires = DateTimeOffset.UtcNow.AddHours(1);
        _jwtTokenGenerator.GenerateToken(Arg.Any<User>())
            .Returns(new GeneratedJwtToken("jwt_token_value", expires));

        var handler = new RegisterUserCommandHandler(
            _userRepository,
            _dbContext,
            _passwordHasher,
            _jwtTokenGenerator);

        var command = new RegisterUserCommand(
            "driver@malume.co.za",
            "Sipho Ndlovu",
            "+27829876543",
            "StrongPass!123",
            MalumeRoles.Driver);

        AuthResponse response = await handler.Handle(command, CancellationToken.None);

        response.AccessToken.ShouldBe("jwt_token_value");
        response.Role.ShouldBe(MalumeRoles.Driver);
        await _userRepository.Received(1).AddAsync(
            Arg.Is<User>(u => u.NormalizedEmail == "DRIVER@MALUME.CO.ZA" && u.PasswordHash == "hashed_pw"),
            Arg.Any<CancellationToken>());
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginHandler_WhenPasswordInvalid_ShouldThrowUnauthorizedException()
    {
        var user = new User(
            "parent@malume.co.za",
            "Parent One",
            "+27820001111",
            "stored_hash",
            UserRole.Parent);

        _userRepository.GetByEmailAsync("PARENT@MALUME.CO.ZA", Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher.VerifyPassword("stored_hash", "wrong_pass").Returns(false);

        var handler = new LoginCommandHandler(_userRepository, _passwordHasher, _jwtTokenGenerator);

        await Should.ThrowAsync<UnauthorizedException>(() =>
            handler.Handle(new LoginCommand("parent@malume.co.za", "wrong_pass"), CancellationToken.None));
    }
}
