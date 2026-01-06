using CarrotDownload.Auth.Interfaces;

namespace CarrotDownload.Maui.Services;

/// <summary>
/// Secure storage implementation using .NET MAUI SecureStorage
/// </summary>
public sealed class SecureStorageService : ISecureStorageService
{
    public async Task SetAsync(string key, string value)
    {
        await SecureStorage.Default.SetAsync(key, value);
    }

    public async Task<string?> GetAsync(string key)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(key);
        }
        catch
        {
            return null;
        }
    }

    public Task RemoveAsync(string key)
    {
        SecureStorage.Default.Remove(key);
        return Task.CompletedTask;
    }

    public Task ClearAllAsync()
    {
        SecureStorage.Default.RemoveAll();
        return Task.CompletedTask;
    }
}
