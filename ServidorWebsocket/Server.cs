using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;

namespace ServidorWebsocket;

public class Server
{
    private readonly HttpListener _listener;
    private Task _acceptConnectionsTask;
    private ConcurrentDictionary<string, WebSocket> Connections = new();

    public Server()
    {
        _listener = new HttpListener();
        
        _listener.Prefixes.Add("http://*:80/");
        
        _listener.Start();
        
        _acceptConnectionsTask = Task.Run(AcceptConnections);
    }

    private async Task AcceptConnections()
    {
        while (true)
        {
            if (!_listener.IsListening)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);
                continue;
            }
            
            var ctx = await _listener.GetContextAsync().ConfigureAwait(false);
            
            
            if (!ctx.Request.IsWebSocketRequest)
            {
                var stringBuilder = new StringBuilder(); 
                var url = stringBuilder.Append(ctx.Request.HttpMethod).Append(' ').Append(ctx.Request.RawUrl);

                ctx.Response.Close();
                continue;
            }
            
            await Task.Run(() =>
            {
                var tokenSource = new CancellationTokenSource();
                var token = tokenSource.Token;
                        
                Task.Run(async () =>
                {
                    try
                    {
                        var wsContext = await ctx.AcceptWebSocketAsync(subProtocol: null);

                        var ip = ctx.Request.RemoteEndPoint.Address + ":" + ctx.Request.RemoteEndPoint.Port;
                        Console.WriteLine("Connection established " + ip);
                        var ws = wsContext.WebSocket;
                        
                        Connections.TryAdd(ip, ws);
                        
                        _ = Task.Run(() => ReadAndEcho(ws, token, ip), tokenSource.Token);
                    }
                    catch (Exception e)
                    {
                        ctx.Response.Close();
                    }
                             
                }, token);

            }).ConfigureAwait(false);
        }
    }

    private async Task ReadAndEcho(WebSocket ws, CancellationToken token, string ip)
    {
        var buffer = new byte[65536];

        while (true)
        {
            var bytes = await ReceiveMessage(ws, token, buffer);

            var echoed = $"[{ip}] {Encoding.UTF8.GetString(bytes.ToArray())}";
            
            if (bytes != null)
            {
                await ws.SendAsync(Encoding.UTF8.GetBytes(echoed), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
                Console.WriteLine("Echoed message");
            }
            else
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);
            }
        }
    }

    private async Task<ArraySegment<byte>> ReceiveMessage(WebSocket ws, CancellationToken token, byte[] buffer)
    {
        // Read stream
        while (true)
        {
            using var ms = new MemoryStream();
            var seg = new ArraySegment<byte>(buffer);

            var result = await ws.ReceiveAsync(seg, token).ConfigureAwait(false);

            if (result.CloseStatus != null)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                throw new WebSocketException("Websocket closed.");
            }

            if (ws.State != WebSocketState.Open)
            {
                throw new WebSocketException("Websocket closed.");
            }

            if (result.Count > 0)
            {
                ms.Write(buffer, 0, result.Count);
            }

            if (result.EndOfMessage)
            {
                Console.WriteLine("Received new message");
                return new ArraySegment<byte>(ms.GetBuffer(), 0, (int) ms.Length);
            }
        }
    }
}