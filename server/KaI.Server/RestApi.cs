using MaxLib.WebServer;
using MaxLib.WebServer.Builder;
using Direction = KaI.Brain.Direction;

namespace KaI.Server;

#pragma warning disable CA1822 // Mark members as static
public class RestApi : Service
{
    [Method(HttpProtocolMethod.Get)]
    [Path("api/v1/move/{direction}")]
    public async Task<string> MoveAsync(
        [Var("direction")]string direction,
        [Get("id")]string id,
        [Get("text")]string text
    )
    {
        var dir = Enum.Parse<Direction>(direction, true);
        await WebSocketConnection.SendToAll(new Events.Command
        {
            Id = id ?? "",
            Direction = dir,
            Text = text
        });
        return $"Moved {direction} with text '{text}'";
    }

    [Method(HttpProtocolMethod.Get)]
    [Path("api/v1/kai/move")]
    public async Task<string> MoveAsync(
        [Get("id")]string id,
        [Get("text")]string text
    )
    {
        if (Program.Classifier is null)
            return "Classifier not initialized.";
        var result = Program.Classifier.Classify(text, Direction.Left | Direction.Right | Direction.Down);
        await WebSocketConnection.SendToAll(new Events.Command
        {
            Id = id ?? "",
            Direction = result.Direction,
            Text = result.Text
        });
        return $"Moved {result.Direction} with confidence {result.Confidence} and text '{result.Text}' from input '{text}'";
    }
}
#pragma warning restore CA1822 // Mark members as static
