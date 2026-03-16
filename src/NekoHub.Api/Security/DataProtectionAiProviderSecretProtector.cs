using Microsoft.AspNetCore.DataProtection;
using NekoHub.Application.Abstractions.Security;

namespace NekoHub.Api.Security;

public sealed class DataProtectionAiProviderSecretProtector(IDataProtectionProvider dataProtectionProvider)
    : IAiProviderSecretProtector
{
    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("NekoHub.AiProviderProfile.ApiKey.v1");

    public string Protect(string secret)
    {
        return _protector.Protect(secret);
    }

    public string Unprotect(string protectedSecret)
    {
        return _protector.Unprotect(protectedSecret);
    }
}
