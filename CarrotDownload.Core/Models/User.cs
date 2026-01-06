using CarrotDownload.Core.Enums;

namespace CarrotDownload.Core.Models;

public sealed class User
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public UserRole Role { get; init; } = UserRole.User;
    public string? Token { get; init; }
    public string? RefreshToken { get; init; }
    public License? License { get; init; }
}

