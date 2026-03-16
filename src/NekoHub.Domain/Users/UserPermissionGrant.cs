namespace NekoHub.Domain.Users;

public sealed class UserPermissionGrant
{
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Permission { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public User? User { get; private set; }

    private UserPermissionGrant()
    {
    }

    public UserPermissionGrant(Guid userId, string permission)
        : this(Guid.CreateVersion7(), userId, permission)
    {
    }

    public UserPermissionGrant(Guid id, Guid userId, string permission)
    {
        Id = id;
        UserId = userId;
        Permission = NormalizeRequired(permission, nameof(permission));
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        return value.Trim();
    }
}
