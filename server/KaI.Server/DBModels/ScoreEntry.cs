using LiteDB;
using KaI.Server.Events;

namespace KaI.Server.DBModels;

class ScoreEntry
{
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();

    public Score Score { get; set; } = new();

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
