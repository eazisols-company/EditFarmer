namespace CarrotDownload.Auth.Interfaces;

public interface ISecureStorageService
{
    /// <summary>
    /// Stores a value securely
    /// </summary>
    Task SetAsync(string key, string value);

    /// <summary>
    /// Retrieves a securely stored value
    /// </summary>
    Task<string?> GetAsync(string key);

    /// <summary>
    /// Removes a securely stored value
    /// </summary>
    Task RemoveAsync(string key);

    /// <summary>
    /// Clears all securely stored values
    /// </summary>
    Task ClearAllAsync();
}
