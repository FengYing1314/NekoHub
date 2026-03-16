using NekoHub.Domain.Users;

namespace NekoHub.Application.Abstractions.Security;

public interface IPasswordHashService
{
    string HashPassword(User user, string password);

    bool VerifyPassword(User user, string password);
}
