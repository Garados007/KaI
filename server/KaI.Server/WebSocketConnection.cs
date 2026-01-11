using MaxLib.WebServer.WebSocket;

namespace KaI.Server;

public class WebSocketConnection : EventConnection
{
    readonly static List<WebSocketConnection> peers = [];
    readonly static ReaderWriterLockSlim peerLock = new();
    readonly Serilog.ILogger logger = Serilog.Log.ForContext<WebSocketConnection>();

    public WebSocketConnection(Stream networkStream, EventFactory factory)
        : base(networkStream, factory)
    {
        try
        {
            peerLock.EnterWriteLock();
            peers.Add(this);
        }
        finally
        {
            peerLock.ExitWriteLock();
        }
        Closed += (_, _) =>
        {
            try
            {
                peerLock.EnterWriteLock();
                peers.Remove(this);
            }
            finally
            {
                peerLock.ExitWriteLock();
            }
        };
    }

    protected override Task ReceiveClose(CloseReason? reason, string? info)
    {
        return Task.CompletedTask;
    }

    private static readonly System.Text.Json.JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true
    };

    protected override Task ReceivedFrame(EventBase @event)
    {
        _ = Task.Run(async () =>
        {
            switch (@event)
            {
                case Events.Command cmd:
                    logger.Information("Received command: \"{cmd}\" => {dir}", cmd.Text, cmd.Direction);
                    break;
                case Events.Foo foo:
                    logger.Information("Foo!");
                    break;
                case Events.Score score:
                {
                    logger.Information("Received score: {score} with combo {combo}", score.ScoreValue, score.Combo);
                    var database = Program.Database;
                    if (database is null)
                    {
                        logger.Warning("Database is not available.");
                        break;
                    }
                    // store the score to the database
                    database.AddScore(score);
                    // submit aggregated score to all clients
                    await SendToAll(new Events.ScoreStats
                    {
                        TodayHighScore = database.TodayHighScore,
                        AlltimeHighScore = database.AlltimeHighScore,
                        TodayHighCombo = database.TodayHighCombo,
                        AlltimeHighCombo = database.AlltimeHighCombo,
                        CurrentScore = score.ScoreValue,
                        CurrentCombo = score.Combo
                    });
                    break;
                }
                case Events.RequestHighScores req:
                {
                    var database = Program.Database;
                    if (database is null)
                    {
                        logger.Warning("Database is not available.");
                        break;
                    }
                    await Send(new Events.ScoreStats
                    {
                        TodayHighScore = database.TodayHighScore,
                        AlltimeHighScore = database.AlltimeHighScore,
                        TodayHighCombo = database.TodayHighCombo,
                        AlltimeHighCombo = database.AlltimeHighCombo,
                        CurrentScore = database.CurrentScore?.ScoreValue ?? 0,
                        CurrentCombo = database.CurrentScore?.Combo ?? 0
                    });
                    break;
                }
            }
        });
        return Task.CompletedTask;
    }

    public async Task Send<T>(T @event)
        where T : EventBase
    {
        await SendFrame(@event).ConfigureAwait(false);
    }

    public static async Task SendToAll<T>(T @event)
        where T : EventBase
    {
        List<Task> sendTasks = [];
        try
        {
            peerLock.EnterReadLock();
            foreach (var peer in peers)
            {
                sendTasks.Add(peer.Send(@event));
            }
        }
        finally
        {
            peerLock.ExitReadLock();
        }
        await Task.WhenAll(sendTasks);
    }
}
