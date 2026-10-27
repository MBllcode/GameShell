using System.Net;
using System.Net.Sockets;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace GameShell.Desktop.RemoteControl;

public sealed record RemoteControlAccessResult(bool Allowed, int StatusCode, string? Error = null);

public static class RemoteControlAuthentication
{
    public static RemoteControlAccessResult CheckRequest(HttpContext context, RemoteControlOptions options)
    {
        if (options.AllowPrivateNetworkOnly && !IsPrivateOrLocalAddress(context.Connection.RemoteIpAddress))
        {
            return new RemoteControlAccessResult(false, StatusCodes.Status403Forbidden, "PrivateNetworkOnly");
        }

        if (string.Equals(context.Request.Path.Value, "/api/v1/health", StringComparison.OrdinalIgnoreCase))
        {
            return new RemoteControlAccessResult(true, StatusCodes.Status200OK);
        }

        return HasValidBearerToken(context.Request.Headers.Authorization, options.AuthenticationToken)
            ? new RemoteControlAccessResult(true, StatusCodes.Status200OK)
            : new RemoteControlAccessResult(false, StatusCodes.Status401Unauthorized, "Unauthorized");
    }

    public static bool HasValidBearerToken(string? authorizationHeader, string expectedToken)
    {
        if (string.IsNullOrWhiteSpace(expectedToken)
            || !AuthenticationHeaderValue.TryParse(authorizationHeader, out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(expectedToken);
        var provided = Encoding.UTF8.GetBytes(authorization.Parameter);
        return expected.Length == provided.Length
            && CryptographicOperations.FixedTimeEquals(expected, provided);
    }

    public static bool IsPrivateOrLocalAddress(IPAddress? address)
    {
        if (address is null)
        {
            return false;
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10
                || bytes[0] == 172 && bytes[1] is >= 16 and <= 31
                || bytes[0] == 192 && bytes[1] == 168;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            var isUniqueLocal = (bytes[0] & 0xFE) == 0xFC;
            var isLinkLocal = bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80;
            return isUniqueLocal || isLinkLocal;
        }

        return false;
    }
}
