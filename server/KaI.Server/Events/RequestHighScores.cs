using System.Text.Json;
using MaxLib.WebServer.WebSocket;

namespace KaI.Server.Events;

class RequestHighScores : EventBase
{
    public override void ReadJsonContent(JsonElement json)
    {
    }

    protected override void WriteJsonContent(Utf8JsonWriter writer)
    {
    }
}
