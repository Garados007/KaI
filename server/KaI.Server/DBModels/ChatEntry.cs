using TwitchLib.Client.Models;

namespace KaI.Server.DBModels;

public class ChatEntry
{
    public string Id => Message?.Id ?? "";

    public ChatMessage? Message { get; set; }

    public Brain.DirectionClassifier.Result? ClassificationResult { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
