namespace NekoHub.Application.Common.Exceptions;

public sealed class UnauthorizedException(string code, string message)
    : AppException(code, message, 401);
