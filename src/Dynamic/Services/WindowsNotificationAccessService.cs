using System.Runtime.InteropServices;
using System.Text;
using Windows.UI.Notifications.Management;

namespace Dynamic.Services;

public enum NotificationAccessResult
{
    PackageIdentityRequired,
    Allowed,
    Denied,
    Unavailable
}

public sealed class WindowsNotificationAccessService
{
    private const int AppModelErrorNoPackage = 15700;

    public async Task<NotificationAccessResult> RequestAccessAsync()
    {
        if (!HasPackageIdentity()) return NotificationAccessResult.PackageIdentityRequired;
        try
        {
            var status = await UserNotificationListener.Current.RequestAccessAsync();
            return status == UserNotificationListenerAccessStatus.Allowed
                ? NotificationAccessResult.Allowed
                : NotificationAccessResult.Denied;
        }
        catch (Exception)
        {
            return NotificationAccessResult.Unavailable;
        }
    }

    private static bool HasPackageIdentity()
    {
        uint length = 0;
        var result = GetCurrentPackageFullName(ref length, null);
        return result != AppModelErrorNoPackage;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, StringBuilder? packageFullName);
}
