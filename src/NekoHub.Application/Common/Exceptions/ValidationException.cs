using System.Net;

namespace NekoHub.Application.Common.Exceptions;

public sealed class ValidationException(string code, string message)
    : AppException(code, message, (int)HttpStatusCode.BadRequest);
