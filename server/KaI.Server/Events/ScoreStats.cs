using System.Text.Json;
using KaI.Server.DBModels;
using MaxLib.WebServer.WebSocket;

namespace KaI.Server.Events;

class ScoreStats : EventBase
{
    public HighScoreValue? TodayHighScore { get; set; }
    public HighScoreValue? AlltimeHighScore { get; set; }
    public HighScoreValue? TodayHighCombo { get; set; }
    public HighScoreValue? AlltimeHighCombo { get; set; }

    public long CurrentScore { get; set; }
    public long CurrentCombo { get; set; }

    public override void ReadJsonContent(JsonElement json)
    {
        // no need to implement deserialization
        throw new NotImplementedException();
    }

    protected override void WriteJsonContent(Utf8JsonWriter writer)
    {
        if (TodayHighScore != null)
        {
            writer.WritePropertyName("todayHighScore");
            JsonSerializer.Serialize(writer, TodayHighScore);
        }
        else writer.WriteNull("todayHighScore");
        if (AlltimeHighScore != null)
        {
            writer.WritePropertyName("alltimeHighScore");
            JsonSerializer.Serialize(writer, AlltimeHighScore);
        }
        else writer.WriteNull("alltimeHighScore");
        if (TodayHighCombo != null)
        {
            writer.WritePropertyName("todayHighCombo");
            JsonSerializer.Serialize(writer, TodayHighCombo);
        }
        else writer.WriteNull("todayHighCombo");
        if (AlltimeHighCombo != null)
        {
            writer.WritePropertyName("alltimeHighCombo");
            JsonSerializer.Serialize(writer, AlltimeHighCombo);
        }
        else writer.WriteNull("alltimeHighCombo");
        writer.WriteNumber("currentScore", CurrentScore);
        writer.WriteNumber("currentCombo", CurrentCombo);
    }
}
