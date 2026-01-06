namespace CarrotDownload.Auth.Interfaces;

public interface IDeviceInfoService
{
    /// <summary>
    /// Gets a unique hardware-based device identifier
    /// </summary>
    string GetDeviceId();

    /// <summary>
    /// Gets the device name (computer name)
    /// </summary>
    string GetDeviceName();

    /// <summary>
    /// Gets the MAC address of the primary network adapter
    /// </summary>
    string GetMacAddress();
}
