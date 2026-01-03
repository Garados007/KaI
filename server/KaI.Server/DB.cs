using KaI.Server.DBModels;
using KaI.Server.Events;
using LiteDB;

namespace KaI.Server;

class DB : IDisposable
{
    public LiteDatabase Database { get; }

    public ILiteCollection<ChatEntry> ChatMessages { get; }

    public ILiteCollection<ScoreEntry> Scores { get; }

    public ILiteCollection<HighScoreValue> HighScores { get; }

    public ILiteCollection<HighScoreValue> HighCombos { get; }

    private HighScoreValue? todayHighScore;
    public HighScoreValue? TodayHighScore => todayHighScore;

    private HighScoreValue? alltimeHighScore;
    public HighScoreValue? AlltimeHighScore => alltimeHighScore;

    private HighScoreValue? todayHighCombo;
    public HighScoreValue? TodayHighCombo => todayHighCombo;

    private HighScoreValue? alltimeHighCombo;
    public HighScoreValue? AlltimeHighCombo => alltimeHighCombo;

    public DB(string path)
    {
        Database = new LiteDatabase(path);
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        // create chat collection
        ChatMessages = Database.GetCollection<ChatEntry>("chat");
        ChatMessages.EnsureIndex(x => x.Id, true);

        // create score collection
        Scores = Database.GetCollection<ScoreEntry>("scores");
        Scores.EnsureIndex(x => x.Id, true);

        // create high score collection
        HighScores = Database.GetCollection<HighScoreValue>("highscores");
        HighScores.EnsureIndex(x => x.Id, true);
        todayHighScore = HighScores.FindOne(x => x.Date == today);
        alltimeHighScore = HighScores.Query()
            .OrderByDescending(x => x.Value)
            .FirstOrDefault();

        // create high combo collection
        HighCombos = Database.GetCollection<HighScoreValue>("highcombos");
        HighCombos.EnsureIndex(x => x.Id, true);
        todayHighCombo = HighCombos.FindOne(x => x.Date == today);
        alltimeHighCombo = HighCombos.Query()
            .OrderByDescending(x => x.Value)
            .FirstOrDefault();
    }

    public void AddScore(Events.Score score)
    {
        var entry = new ScoreEntry
        {
            Score = score
        };
        Scores.Insert(entry);
        // check if the date has been changed for today's high score
        UpdateScoreValue(ref todayHighScore, ref alltimeHighScore, HighScores, entry.Timestamp, score.LastCommand, score.ScoreValue);
        UpdateScoreValue(ref todayHighCombo, ref alltimeHighCombo, HighCombos, entry.Timestamp, score.LastCommand, score.Combo);
    }

    private static void UpdateScoreValue(ref HighScoreValue? todayVar, ref HighScoreValue? alltimeVar, ILiteCollection<HighScoreValue> collection, DateTime now, string? chatId, long newValue)
    {
        // load today's high score if not loaded yet or date changed
        var today = DateOnly.FromDateTime(now);
        if (todayVar is null || todayVar.Date != today)
        {
            todayVar = collection.FindOne(x => x.Date == today);
        }
        // add new high score if not existing
        if (todayVar is null)
        {
            todayVar = new HighScoreValue
            {
                ChatId = chatId,
                Value = newValue,
                AchievedAt = now
            };
            collection.Insert(todayVar);
        }
        // update high score if new value is higher
        if (newValue > todayVar.Value)
        {
            todayVar.ChatId = chatId;
            todayVar.Value = newValue;
            todayVar.AchievedAt = now;
            collection.Update(todayVar);
        }
        // update all-time high score if new value is higher
        if (alltimeVar is null || newValue > alltimeVar.Value)
        {
            alltimeVar = todayVar;
        }
    }

    public void Dispose()
    {
        Database.Dispose();
        GC.SuppressFinalize(this);
    }
}
