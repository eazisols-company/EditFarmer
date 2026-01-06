using CarrotDownload.Auth.Models;

namespace CarrotDownload.Auth.Interfaces;

public interface IAuthService
{
    /// <summary>
    /// Event triggered when user information is updated
    /// </summary>
    event EventHandler<string>? UserInfoUpdated;

    /// <summary>
    /// Authenticates a user with email and password
    /// </summary>
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a new user account
    /// </summary>
    Task<AuthResponse> SignUpAsync(SignUpRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs out the current user and clears stored credentials
    /// </summary>
    Task LogoutAsync();

    /// <summary>
    /// Checks if user is currently authenticated
    /// </summary>
    Task<bool> IsAuthenticatedAsync();

    /// <summary>
    /// Gets the current user's data
    /// </summary>
    Task<UserData?> GetCurrentUserAsync();

    /// <summary>
    /// Gets the stored authentication token
    /// </summary>
    Task<string?> GetTokenAsync();

    /// <summary>
    /// Verifies if the provided password matches the user's current password
    /// </summary>
    Task<bool> VerifyPasswordAsync(string email, string password);

    /// <summary>
    /// Changes the user's password
    /// </summary>
    Task<bool> ChangePasswordAsync(string email, string oldPassword, string newPassword);

    /// <summary>
    /// Deletes the user's account
    /// </summary>
    Task<bool> DeleteAccountAsync(string email);

    /// <summary>
    /// Updates user's basic information
    /// </summary>
    Task<bool> UpdateUserInfoAsync(string fullName, string email);
}
