using Microsoft.Extensions.Logging;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using Serilog;
using TwitchLib.Client.Extensions;
using TwitchLib.Api;
using TwitchLib.Api.Services;
using TwitchLib.Api.Services.Events;

namespace KaI.Server;

public class TwitchConnection : IAsyncDisposable
{
    private readonly TwitchClient client;
    private readonly Serilog.ILogger log = Log.ForContext<TwitchConnection>();
    // private readonly TwitchAPI api;

    public static async Task<TwitchConnection> CreateAsync(string clientId)
    {
        var connection = new TwitchConnection(clientId);

        // var token = await connection.api.ThirdParty.AuthorizationFlow.GetAccessTokenAsync();
        // Log.Information("Twitch API Access Token acquired: {token}", token);

        // connection.api.Auth.

        var credentials = new ConnectionCredentials(new Capabilities(commands: false));
        connection.client.Initialize(credentials);
        await connection.ConnectAsync();
        return connection;
    }

    private TwitchConnection(string clientId)
    {
        var logger = Log.ForContext<TwitchConnection>();
        logger.BindProperty("TwitchClient", true, false, out _);
        // api = new TwitchAPI(loggerFactory: new LoggerFactory().AddSerilog(logger));
        // api.Settings.ClientId = clientId;

        client = new TwitchClient(loggerFactory: new LoggerFactory().AddSerilog(logger));
        client.OnConnected += Client_OnConnected;
        client.OnJoinedChannel += Client_OnJoinedChannel;
        client.OnMessageReceived += Client_OnMessageReceived;
    }

    public async Task ConnectAsync()
    {
        await client.ConnectAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await client.DisconnectAsync();
        GC.SuppressFinalize(this);
    }

    private async Task Client_OnConnected(object? sender, OnConnectedEventArgs e)
    {
        log.Information("Twitch Bot connected to Twitch as {botName}", e.BotUsername);
        await client.JoinChannelAsync("garados007");
    }

    private async Task Client_OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
    {
        log.Information("Twitch Bot joined channel: {channel}", e.Channel);
    }

    private async Task Client_OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        if (e.ChatMessage.ChatReply != null)
            return; // ignore replies
        log.Information("Twitch Bot received message from {username}: {message}", e.ChatMessage.Username, e.ChatMessage.Message);
        if (Program.Classifier is null)
            return;
        var result = Program.Classifier.Classify(e.ChatMessage.Message, Brain.Direction.Left | Brain.Direction.Right | Brain.Direction.Down);
        Program.Database?.ChatMessages.Insert(new DBModels.ChatEntry
        {
            Message = e.ChatMessage,
            ClassificationResult = result
        });
        await WebSocketConnection.SendToAll(new Events.Command
        {
            Id = e.ChatMessage.Id,
            Direction = result.Direction,
            Text = result.Text
        });
    }
}
