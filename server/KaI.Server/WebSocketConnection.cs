using MaxLib.WebServer.WebSocket;

namespace KaI.Server;

public class WebSocketConnection : EventConnection
{
    readonly static List<WebSocketConnection> peers = [];
    readonly static ReaderWriterLockSlim peerLock = new();

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

    protected override Task ReceivedFrame(EventBase @event)
    {
        _ = Task.Run(async () =>
        {
            switch (@event)
            {
                case Events.Command cmd:
                    Serilog.Log.Information("Received command: \"{cmd}\" => {dir}", cmd.Text, cmd.Direction);
                    break;
                case Events.Foo foo:
                    Serilog.Log.Information("Foo!");
                    break;
                case Events.Score score:
                    Serilog.Log.Information("Received score: {score} with combo {combo}", score.ScoreValue, score.Combo);
                    break;
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
