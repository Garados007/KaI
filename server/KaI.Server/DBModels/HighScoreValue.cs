namespace KaI.Server.DBModels;

using System.Text.Json.Serialization;
using LiteDB;

class HighScoreValue
{
    [JsonIgnore]
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();

    public string? ChatId { get; set; }

    public long Value { get; set; }

    public DateTime AchievedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public DateOnly Date => DateOnly.FromDateTime(AchievedAt);
}
