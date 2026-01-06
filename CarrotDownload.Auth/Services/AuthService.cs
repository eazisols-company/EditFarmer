using CarrotDownload.Auth.Interfaces;
using CarrotDownload.Auth.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace CarrotDownload.Auth.Services;

public sealed class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IDeviceInfoService _deviceInfoService;
    private readonly ISecureStorageService _secureStorage;
    private readonly CarrotDownload.Database.CarrotMongoService _mongoService;
    
    public event EventHandler<string>? UserInfoUpdated;
    
    private const string TokenKey = "auth_token";
    private const string UserDataKey = "user_data";

    public AuthService(
        HttpClient httpClient,
        IDeviceInfoService deviceInfoService,
        ISecureStorageService secureStorage,
        CarrotDownload.Database.CarrotMongoService mongoService)
    {
        _httpClient = httpClient;
        _deviceInfoService = deviceInfoService;
        _secureStorage = secureStorage;
        _mongoService = mongoService;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Add device information
            request.DeviceId = _deviceInfoService.GetDeviceId();
            request.DeviceName = _deviceInfoService.GetDeviceName();

            var response = await _httpClient.PostAsJsonAsync("/auth/login", request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return new AuthResponse
                {
                    Success = false,
                    Message = $"Login failed: {response.StatusCode}"
                };
            }

            var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken);
            
            if (authResponse?.Success == true && !string.IsNullOrEmpty(authResponse.Token))
            {
                // Store token and user data securely
                await _secureStorage.SetAsync(TokenKey, authResponse.Token);
                
                if (authResponse.User != null)
                {
                    var userData = JsonSerializer.Serialize(authResponse.User);
                    await _secureStorage.SetAsync(UserDataKey, userData);
                }
            }

            return authResponse ?? new AuthResponse { Success = false, Message = "Invalid response" };
        }
        catch (HttpRequestException ex)
        {
            return new AuthResponse
            {
                Success = false,
                Message = $"Network error: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new AuthResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<AuthResponse> SignUpAsync(SignUpRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Add device information
            request.DeviceId = _deviceInfoService.GetDeviceId();
            request.DeviceName = _deviceInfoService.GetDeviceName();

            var response = await _httpClient.PostAsJsonAsync("/auth/signup", request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return new AuthResponse
                {
                    Success = false,
                    Message = $"Sign up failed: {response.StatusCode}"
                };
            }

            var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken);
            
            if (authResponse?.Success == true && !string.IsNullOrEmpty(authResponse.Token))
            {
                // Store token and user data securely
                await _secureStorage.SetAsync(TokenKey, authResponse.Token);
                
                if (authResponse.User != null)
                {
                    var userData = JsonSerializer.Serialize(authResponse.User);
                    await _secureStorage.SetAsync(UserDataKey, userData);
                }
            }

            return authResponse ?? new AuthResponse { Success = false, Message = "Invalid response" };
        }
        catch (HttpRequestException ex)
        {
            return new AuthResponse
            {
                Success = false,
                Message = $"Network error: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new AuthResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public async Task LogoutAsync()
    {
        await _secureStorage.RemoveAsync(TokenKey);
        await _secureStorage.RemoveAsync(UserDataKey);
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetTokenAsync();
        return !string.IsNullOrEmpty(token);
    }

    public async Task<UserData?> GetCurrentUserAsync()
    {
        var userDataJson = await _secureStorage.GetAsync(UserDataKey);
        
        if (string.IsNullOrEmpty(userDataJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<UserData>(userDataJson);
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetTokenAsync()
    {
        return await _secureStorage.GetAsync(TokenKey);
    }

    public async Task<bool> VerifyPasswordAsync(string email, string password)
    {
        var user = await _mongoService.GetUserByEmailAsync(email);
        if (user == null) return false;
        return await _mongoService.VerifyUserPasswordAsync(user.Id, password);
    }

    public async Task<bool> ChangePasswordAsync(string email, string oldPassword, string newPassword)
    {
        var user = await _mongoService.GetUserByEmailAsync(email);
        if (user == null) return false;
        
        var verified = await _mongoService.VerifyUserPasswordAsync(user.Id, oldPassword);
        if (!verified) return false;

        return await _mongoService.ResetUserPasswordAsync(user.Id, newPassword);
    }

    public async Task<bool> DeleteAccountAsync(string email)
    {
        // TODO: Call actual API endpoint
        // var response = await _httpClient.DeleteAsync($"/auth/account/{email}"); // or similar
        // return response.IsSuccessStatusCode;
        
        return true;
    }

    public async Task<bool> UpdateUserInfoAsync(string fullName, string email)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null) return false;

        // 1. Update in MongoDB via mongo service
        var success = await _mongoService.UpdateUserBasicInfoAsync(currentUser.Id, fullName, email);
        
        if (success)
        {
            // 2. Update local cached data in SecureStorage
            currentUser.FullName = fullName;
            currentUser.Email = email;
            var userData = JsonSerializer.Serialize(currentUser);
            await _secureStorage.SetAsync(UserDataKey, userData);

            // Notify observers that user info has changed
            UserInfoUpdated?.Invoke(this, fullName);
        }

        return success;
    }
}
