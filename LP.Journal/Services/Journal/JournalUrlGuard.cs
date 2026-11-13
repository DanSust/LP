using System.Net;

namespace LP.Journal.Services.Journal;

/// <summary>
/// Проверяет, что внешний адрес публичный. Локальные и служебные адреса
/// запрещены, чтобы краулер не мог обратиться к внутренней сети.
/// </summary>
public static class JournalUrlGuard
{
    public static bool TryGetPublicHttpUri(string? value, out Uri? uri)
    {
        uri = null;

        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var parsed) ||
            !IsAllowed(parsed))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    public static bool IsAllowed(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (uri.IsLoopback ||
            uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!IPAddress.TryParse(uri.Host, out var address))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] switch
            {
                10 => false,
                127 => false,
                0 => false,
                169 when bytes[1] == 254 => false,
                172 when bytes[1] is >= 16 and <= 31 => false,
                192 when bytes[1] == 168 => false,
                _ => true
            };
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return !address.Equals(IPAddress.IPv6Loopback) &&
                   !address.IsIPv6LinkLocal &&
                   !address.IsIPv6SiteLocal;
        }

        return true;
    }
}
