using System.Text.Json;
using MaxLib.WebServer.WebSocket;

using Direction = KaI.Brain.Direction;

namespace KaI.Server.Events;

class Command : EventBase
{
    public string Id { get; set; } = "";

    public string Text { get; set; } = "";

    public Direction Direction { get; set; }

    public override void ReadJsonContent(JsonElement json)
    {
        Id = json.GetProperty("id").GetString() ?? "";
        Text = json.GetProperty("text").GetString() ?? "";
        if(Enum.TryParse<Direction>(json.GetProperty("direction").GetString(), true, out var dir))
            Direction = dir;
    }

    protected override void WriteJsonContent(Utf8JsonWriter writer)
    {
        writer.WriteString("id", Id);
        writer.WriteString("text", Text);
        writer.WriteString("direction", Direction.ToString().ToLowerInvariant());
    }
}

class Foo : EventBase
{
    public override void ReadJsonContent(JsonElement json)
    {
    }

    protected override void WriteJsonContent(Utf8JsonWriter writer)
    {
    }
}
