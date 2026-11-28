namespace Malume.Application.Abstractions.Identity;

public interface IPasswordHasher
{
    string HashPassword(string password);

    bool VerifyPassword(string passwordHash, string providedPassword);
}
