using MaxLib.WebServer;
using Serilog;
using Serilog.Events;
using Serilog.Filters;
using System.CommandLine;

[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(KaI.Server.Program))]

namespace KaI.Server;

class Settings
{
    public int Port { get; set; } = 8005;

    public DirectoryInfo? DataDirectory { get; set; }

    public DirectoryInfo? CacheDirectory { get; set; }

    public FileInfo? Network { get; set; }

    public string? TwitchApiClientId { get; set; }
};

class Program
{
    public static Brain.DirectionClassifier? Classifier;

    public static DB? Database;

    public static Settings Settings { get; private set; } = new();

    private static TwitchConnection? twitchConnection;

    private static readonly Serilog.ILogger log = Log.ForContext<Program>();

    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("KaI Server Application");

        // Port option to define the server port
        var portOption = new Option<int>("--port", "-p")
        {
            Description = "The port on which the server will listen.",
            Required = false,
            DefaultValueFactory = _ => Settings.Port,
        };
        rootCommand.Options.Add(portOption);

        // data directory which will be mirrored to the server
        var dataDirOption = new Option<DirectoryInfo>("--data-dir", "-d")
        {
            Description = "The data directory which will be used by the server.",
            Required = false,
        };
        dataDirOption.Validators.Add(result =>
        {
            var dir = result.GetValueOrDefault<DirectoryInfo>();
            if (dir != null && !dir.Exists)
                result.AddError($"The specified data directory '{dir.FullName}' does not exist.");
        });
        rootCommand.Options.Add(dataDirOption);

        // cache directory which will be used by the server
        var cacheDirOption = new Option<DirectoryInfo>("--cache-dir", "-c")
        {
            Description = "The cache directory which will be used by the server.",
            Required = false,
        };
        cacheDirOption.Validators.Add(result =>
        {
            var dir = result.GetValueOrDefault<DirectoryInfo>();
            Console.WriteLine($"Cache dir: {dir}");
            if (dir != null && !dir.Exists)
                result.AddError($"The specified cache directory '{dir.FullName}' does not exist.");
        });
        rootCommand.Options.Add(cacheDirOption);

        // neuronal network file path
        var networkOption = new Option<FileInfo>("--network")
        {
            Description = "The neuronal network file to load. If this file does not exists, a new network will be created.",
            Required = false,
        };
        rootCommand.Options.Add(networkOption);

        // twitch api client id option
        var twitchClientIdOption = new Option<string>("--twitch-client-id")
        {
            Description = "The Twitch API Client ID to use for Twitch integrations.",
            Required = false,
        };
        rootCommand.Options.Add(twitchClientIdOption);

        // finalize root command
        rootCommand.SetAction(async result => await RunServerAsync(new Settings
        {
            Port = result.GetValue(portOption),
            DataDirectory = result.GetValue(dataDirOption),
            CacheDirectory = result.GetValue(cacheDirOption),
            Network = result.GetValue(networkOption),
            TwitchApiClientId = result.GetValue(twitchClientIdOption),
        }));

        // parse the finalized configuration and run, this also handles help and version commands
        return await rootCommand.Parse(args).InvokeAsync();
    }

    static event Func<Task>? ApplicationUpdated;

    public static void UpdateApplication(Type[]? types)
    {
        Console.WriteLine("Application update requested.");
        if (ApplicationUpdated != null)
            _ = ApplicationUpdated.Invoke();
    }

    private static List<WebService> GetServices(MaxLib.WebServer.Server server)
    {
        List<WebService> services = [];
        foreach (var service in server.WebServiceGroups.SelectMany(g => g.Value.GetAll<WebService>()))
        {
            services.Add(service);
        }
        return services;
    }

    static async Task SetupBrain(Settings settings)
    {
        Brain.DirectionClassifier? classifier;
        if(settings.CacheDirectory is null && settings.Network is null)
        {
            // classifier = Brain.DirectionClassifier.CreateNew();
            // await classifier.Training();
            // Classifier = classifier;
            log.Error("No cache directory specified, cannot load or save trained classifier.");
            return;
        }
        var networkFile = settings.Network ?? new FileInfo(Path.Combine(settings.CacheDirectory!.FullName, "trained.nnet"));
        classifier = Brain.DirectionClassifier.LoadFromFile(networkFile);
        if (classifier is null)
        {
            classifier = Brain.DirectionClassifier.CreateNew();
            await classifier.Training();
            classifier.SaveToFile(networkFile);
        }
        Classifier = classifier;
    }

    static async Task<int> RunServerAsync(Settings settings)
    {
        Settings = settings;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Filter.ByExcluding(x =>
                (x.Level == LogEventLevel.Verbose || x.Level == LogEventLevel.Debug) &&
                x.Properties.GetValueOrDefault("SourceContext") is ScalarValue sv &&
                sv.Value is string sc &&
                sc.StartsWith("TwitchLib."))
            .WriteTo.Console(LogEventLevel.Verbose,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        WebServerLog.LogPreAdded += WebServerLog_LogPreAdded;

        using var server = new MaxLib.WebServer.Server(new WebServerSettings(settings.Port, 5000));
        server.InitialDefault();
        var initialServices = GetServices(server);

        static async ValueTask SetupServer(Settings settings, MaxLib.WebServer.Server server, List<WebService> initialServices, bool fromUpdate)
        {
            Settings = settings;

            if (fromUpdate)
            {
                log.Information("Application updated, clearing old services...");
                foreach (var service in GetServices(server).Except(initialServices))
                {
                    if(service is MaxLib.WebServer.WebSocket.WebSocketService webSocketService)
                    {
                        foreach (var endpoint in webSocketService.Endpoints.OfType<WebSocketEndpoint>())
                        {
                            endpoint.Reload();
                        }
                        continue;
                    }
                    server.RemoveWebService(service);
                    service.Dispose();
                }
                log.Information("Application updated, starting new services...");
            }
            else
            {
                // add websocket service
                var ws = new MaxLib.WebServer.WebSocket.WebSocketService();
                ws.Add(new WebSocketEndpoint());
                server.AddWebService(ws);
            }

            // add data directory if specified
            if (settings.DataDirectory != null)
            {
                var mapper = new MaxLib.WebServer.Services.LocalIOMapper();
                mapper.AddFileMapping("data", settings.DataDirectory.FullName);
                server.AddWebService(mapper);
                log.Information("Data directory '{dir}' added at request path '/data'.",
                    settings.DataDirectory.FullName);
            }

            // add rest api service
            var restApi = MaxLib.WebServer.Builder.Service.Build<RestApi>();
            if(restApi is not null)
            {
                server.AddWebService(restApi);
                log.Information("Rest API service added at request path '/api'.");
            }

            if (fromUpdate)
                log.Information("Application update completed.");
            _ = Task.Run(async () => await SetupBrain(settings));

            if (twitchConnection is not null)
            {
                await twitchConnection.DisposeAsync();
            }
            if (!string.IsNullOrEmpty(settings.TwitchApiClientId))
            {
                twitchConnection = await TwitchConnection.CreateAsync(settings.TwitchApiClientId);
            }
        }

        Database?.Dispose();
        if (settings.CacheDirectory != null)
        {
            await MimeType.LoadMimeTypesForExtensions(true, Path.Combine(settings.CacheDirectory.FullName, "mime-types.json"));
            Database = new DB(Path.Combine(settings.CacheDirectory.FullName, "kai-database.litedb"));
        }
        else
        {
            await MimeType.LoadMimeTypesForExtensions(false, null);
            Database = null;
        }

        await SetupServer(settings, server, initialServices, false);
        ApplicationUpdated += async () => await SetupServer(settings, server, initialServices, true);

        await server.RunAsync(cancelFromConsoleInput: true);

        return 0;
    }

    private static readonly MessageTemplate serilogMessageTemplate =
            new Serilog.Parsing.MessageTemplateParser().Parse(
                "{infoType}: {info}"
            );

    private static void WebServerLog_LogPreAdded(ServerLogArgs eventArgs)
    {
        eventArgs.Discard = true;
        Log.Write(new LogEvent(
            eventArgs.LogItem.Date,
            eventArgs.LogItem.Type switch
            {
                ServerLogType.Debug => LogEventLevel.Verbose,
                ServerLogType.Information => LogEventLevel.Debug,
                ServerLogType.Error => LogEventLevel.Error,
                ServerLogType.FatalError => LogEventLevel.Fatal,
                _ => LogEventLevel.Information,
            },
            null,
            serilogMessageTemplate,
            [
                new LogEventProperty("infoType", new ScalarValue(eventArgs.LogItem.InfoType)),
                new LogEventProperty("info", new ScalarValue(eventArgs.LogItem.Information)),
                new LogEventProperty("SourceContext", new ScalarValue(eventArgs.LogItem.SenderType))
            ]
        ));
    }
}
