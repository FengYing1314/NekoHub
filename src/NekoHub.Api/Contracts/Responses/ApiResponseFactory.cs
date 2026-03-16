namespace NekoHub.Api.Contracts.Responses;

public static class ApiResponseFactory
{
    public static ApiResponse<T> Success<T>(T data) => new(data);
}
