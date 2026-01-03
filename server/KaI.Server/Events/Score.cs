using System.Text.Json;
using MaxLib.WebServer.WebSocket;

namespace KaI.Server.Events;

class Score : EventBase
{
    public string? LastCommand { get; set; }

    public long ScoreValue { get; set; }

    public long Combo { get; set; }

    public override void ReadJsonContent(JsonElement json)
    {
        LastCommand = json.GetProperty("lastCommand").GetString();
        ScoreValue = json.GetProperty("score").GetInt64();
        Combo = json.GetProperty("combo").GetInt64();
    }

    protected override void WriteJsonContent(Utf8JsonWriter writer)
    {
        writer.WriteString("lastCommand", LastCommand);
        writer.WriteNumber("score", ScoreValue);
        writer.WriteNumber("combo", Combo);
    }
}
