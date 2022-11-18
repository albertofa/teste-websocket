using System.Net.WebSockets;
using System.Text;
using ServidorWebsocket;

var server = new Server();

await Task.Delay(1000);

ClientWebSocket _clientWs = new ();

var token = new CancellationToken();
await _clientWs.ConnectAsync(new Uri("ws://localhost:8380"), token);

var teste = "echo echo 2";
var testeBytes = Encoding.UTF8.GetBytes(teste);
await _clientWs.SendAsync(new ArraySegment<byte>(testeBytes, 0, testeBytes.Length), WebSocketMessageType.Text, true, token).ConfigureAwait(false);

using var dataMs = new MemoryStream();
var buffer = new byte[65536];
byte[] data = null;
buffer = new byte[buffer.Length];
var bufferSegment = new ArraySegment<byte>(buffer);

var result = await _clientWs.ReceiveAsync(bufferSegment, token);

while (_clientWs.State == WebSocketState.Open)
{
    if (result.Count > 0)
    {
        await dataMs.WriteAsync(buffer.AsMemory(0, result.Count), token);
    }

    if (result.EndOfMessage)
    {
        data = dataMs.ToArray();
        break;
    }
}

Console.WriteLine("Message received: " + Encoding.UTF8.GetString(data));
while (true)
{
    
}