using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace LearnStack.Services;

/// <summary>
/// A <see cref="SocketsHttpHandler.ConnectCallback"/> that resolves the target host exactly
/// once, rejects any resolved address that is private/loopback, and connects directly to the
/// validated IP address. This closes the DNS-rebinding TOCTOU gap: without it, a short-TTL DNS
/// record could resolve to a public address during application-level validation and to a
/// private/internal address by the time the actual TCP connection is established.
/// </summary>
internal static class SafeSocketConnectCallback
{
    public static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var port = context.DnsEndPoint.Port;

        IPAddress[] addresses;
        if (IPAddress.TryParse(host, out var literalAddress))
        {
            addresses = new[] { literalAddress };
        }
        else
        {
            addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        }

        var safeAddress = Array.Find(addresses, a => !OpenGraphService.IsPrivateOrLoopbackIpAddress(a));

        if (safeAddress is null)
        {
            throw new InvalidOperationException(
                $"Refusing to connect to host '{host}': no public IP address could be resolved.");
        }

        var socket = new Socket(safeAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };

        try
        {
            await socket.ConnectAsync(safeAddress, port, cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
