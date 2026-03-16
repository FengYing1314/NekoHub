namespace NekoHub.Domain.Users;

public sealed class User
{
    public Guid Id { get; private set; }

    public string Username { get; private set; } = string.Empty;

    public string NormalizedUsername { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? LastLoginAtUtc { get; private set; }

    public ICollection<RefreshToken> RefreshTokens { get; private set; } = [];

    public ICollection<UserPermissionGrant> PermissionGrants { get; private set; } = [];

    private User()
    {
    }

    public User(
        Guid id,
        string username,
        string passwordHash,
        UserRole role,
        bool isActive = true)
    {
        Id = id;
        SetUsername(username);
        SetPasswordHash(passwordHash);
        Role = role;
        IsActive = isActive;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public void Rename(string username)
    {
        SetUsername(username);
        Touch();
    }

    public void UpdatePasswordHash(string passwordHash)
    {
        AssignPasswordHash(passwordHash);
        Touch();
    }

    public void SetPasswordHash(string passwordHash)
    {
        UpdatePasswordHash(passwordHash);
    }

    public void SetRole(UserRole role)
    {
        Role = role;
        Touch();
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        Touch();
    }

    public void MarkLogin()
    {
        LastLoginAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = LastLoginAtUtc.Value;
    }

    public void RecordLogin()
    {
        MarkLogin();
    }

    private void SetUsername(string username)
    {
        Username = NormalizeRequired(username, nameof(username));
        NormalizedUsername = Username.ToUpperInvariant();
    }

    private void AssignPasswordHash(string passwordHash)
    {
        PasswordHash = NormalizeRequired(passwordHash, nameof(passwordHash));
    }

    private void Touch()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
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
