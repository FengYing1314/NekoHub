using System.Net;

namespace NekoHub.Application.Common.Exceptions;

public sealed class NotFoundException(string code, string message)
    : AppException(code, message, (int)HttpStatusCode.NotFound);
