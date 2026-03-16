using System.Net;

namespace NekoHub.Application.Common.Exceptions;

public sealed class ForbiddenException(string code, string message)
    : AppException(code, message, (int)HttpStatusCode.Forbidden);
