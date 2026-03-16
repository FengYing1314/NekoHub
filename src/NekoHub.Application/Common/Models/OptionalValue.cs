namespace NekoHub.Application.Common.Models;

public readonly record struct OptionalValue<T>(bool IsSet, T? Value)
{
    public static OptionalValue<T> Unspecified => default;

    public static OptionalValue<T> From(T? value)
    {
        return new OptionalValue<T>(true, value);
    }
}
