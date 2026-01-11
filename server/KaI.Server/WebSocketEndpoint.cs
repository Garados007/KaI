using MaxLib.WebServer;
using MaxLib.WebServer.WebSocket;

namespace KaI.Server;

public class WebSocketEndpoint : WebSocketEndpoint<WebSocketConnection>
{
    public override string? Protocol => null;

    private EventFactory factory = new();

    public WebSocketEndpoint()
    {
        SetupFactory();
    }

    private void SetupFactory()
    {
        factory = new();
        // fill the factory with the known event types
        factory.Add<Events.Command>();
        factory.Add<Events.Foo>();
        factory.Add<Events.Score>();
        factory.Add<Events.RequestHighScores>();
    }

    public void Reload()
    {
        Serilog.Log.Information("Reloading WebSocket events...");
        SetupFactory();
    }

    protected override WebSocketConnection? CreateConnection(Stream stream, HttpRequestHeader header)
    {
        if (header.Location.DocumentPathTiles.Length != 1)
            return null;
        if (!header.Location.DocumentPathTiles[0].Equals("ws", StringComparison.InvariantCultureIgnoreCase))
            return null;
        return new WebSocketConnection(
            stream,
            factory
        );
    }
}
