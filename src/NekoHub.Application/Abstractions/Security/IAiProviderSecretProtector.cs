namespace NekoHub.Application.Abstractions.Security;

public interface IAiProviderSecretProtector
{
    string Protect(string secret);

    string Unprotect(string protectedSecret);
}
