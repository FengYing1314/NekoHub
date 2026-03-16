using System.Net;

namespace NekoHub.Application.Common.Exceptions;

public sealed class ConflictException(string code, string message)
    : AppException(code, message, (int)HttpStatusCode.Conflict);
