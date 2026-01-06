using CarrotDownload.Auth.Interfaces;
using System.Management;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;

namespace CarrotDownload.Auth.Services;

public sealed class DeviceInfoService : IDeviceInfoService
{
    private string? _cachedDeviceId;

    public string GetDeviceId()
    {
        if (!string.IsNullOrEmpty(_cachedDeviceId))
            return _cachedDeviceId;

        // User requested specifically to bind with MAC ID
        _cachedDeviceId = GetMacAddress();
        
        return _cachedDeviceId;
    }

    public string GetDeviceName()
    {
        return Environment.MachineName;
    }

    public string GetMacAddress()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            
            Console.WriteLine($"[DeviceInfo] Found {interfaces.Length} network interfaces");
            foreach (var i in interfaces)
            {
                Console.WriteLine($"[DeviceInfo] Interface: {i.Description}, Type: {i.NetworkInterfaceType}, Status: {i.OperationalStatus}");
            }

            // Prioritize Ethernet and WiFi to find the stable physical address
            var nic = interfaces
                .Where(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet || 
                             ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up) // Prefer active connections for reliability
                .Where(ni => !ni.Description.ToLower().Contains("virtual") && 
                             !ni.Description.ToLower().Contains("pseudo") &&
                             !ni.Description.ToLower().Contains("vpn")) // Filter out virtual adapters
                .OrderBy(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 0 : 1) // Prefer Ethernet
                .FirstOrDefault();

            if (nic != null)
            {
                Console.WriteLine($"[DeviceInfo] Selected primary NIC: {nic.Description}");
            }
            else
            {
                Console.WriteLine("[DeviceInfo] Primary NIC not found, trying fallback...");
                // Fallback: If no active Ethernet/WiFi, just take the first valid non-loopback
                nic = interfaces
                    .Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .FirstOrDefault();
                
                if (nic != null) Console.WriteLine($"[DeviceInfo] Selected fallback NIC: {nic.Description}");
            }

            if (nic != null)
            {
                var address = nic.GetPhysicalAddress().ToString();
                Console.WriteLine($"[DeviceInfo] Raw MAC: {address}");
                
                // Format nicely: XX-XX-XX-XX-XX-XX
                if (address.Length == 12)
                {
                    return string.Join("-", Enumerable.Range(0, 6)
                        .Select(i => address.Substring(i * 2, 2)));
                }
                return address;
            }
            
            Console.WriteLine("[DeviceInfo] No suitable NIC found.");
            return "UNKNOWN_MAC";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DeviceInfo] Error getting MAC: {ex.Message}");
            return "UNKNOWN_ERROR";
        }
    }

    // Kept for potential future use or alternative binding strategies
    private string GetProcessorId()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
                foreach (ManagementObject obj in searcher.Get())
                {
                    return obj["ProcessorId"]?.ToString() ?? string.Empty;
                }
            }
        }
        catch
        {
            // Fallback
        }

        return string.Empty;
    }

    private string GetMotherboardId()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
                foreach (ManagementObject obj in searcher.Get())
                {
                    return obj["SerialNumber"]?.ToString() ?? string.Empty;
                }
            }
        }
        catch
        {
            // Fallback
        }

        return string.Empty;
    }

    private static string GenerateHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
