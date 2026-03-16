namespace NekoHub.Api.Contracts.Responses;

public sealed record ApiErrorResponse(ApiError Error);

public sealed record ApiError(string Code, string Message, string? TraceId = null, int? Status = null);
