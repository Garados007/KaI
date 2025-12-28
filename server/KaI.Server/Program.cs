using MaxLib.WebServer;
using Serilog;
using Serilog.Events;
using System.CommandLine;

[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(KaI.Server.Program))]

namespace KaI.Server;

class Settings
{
    public int Port { get; set; } = 8005;

    public DirectoryInfo? DataDirectory { get; set; }

    public DirectoryInfo? CacheDirectory { get; set; }
};

class Program
{
    public static Brain.DirectionClassifier? Classifier;

    static async Task<int> Main(string[] args)
    {
        Settings settings = new();

        var rootCommand = new RootCommand("KaI Server Application");

        // Port option to define the server port
        var portOption = new Option<int>("--port", "-p")
        {
            Description = "The port on which the server will listen.",
            Required = false,
            DefaultValueFactory = _ => settings.Port,
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
            if (dir != null && !dir.Exists)
                result.AddError($"The specified cache directory '{dir.FullName}' does not exist.");
        });
        rootCommand.Options.Add(cacheDirOption);

        // finalize root command
        rootCommand.SetAction(async result => await RunServerAsync(new Settings
        {
            Port = result.GetValue(portOption),
            DataDirectory = result.GetValue(dataDirOption),
            CacheDirectory = result.GetValue(cacheDirOption),
        }));

        // parse the finalized configuration and run, this also handles help and version commands
        return await rootCommand.Parse(args).InvokeAsync();
    }

    static event Action? ApplicationUpdated;

    public static void UpdateApplication(Type[]? _)
    {
        Console.WriteLine("Application update requested.");
        ApplicationUpdated?.Invoke();
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
        if(settings.CacheDirectory is null)
        {
            // classifier = Brain.DirectionClassifier.CreateNew();
            // await classifier.Training();
            // Classifier = classifier;
            Serilog.Log.Error("No cache directory specified, cannot load or save trained classifier.");
            return;
        }
        var fileInfo = new FileInfo(Path.Combine(settings.CacheDirectory.FullName, "trained.nnet"));
        classifier = Brain.DirectionClassifier.LoadFromFile(fileInfo);
        if (classifier is null)
        {
            classifier = Brain.DirectionClassifier.CreateNew();
            await classifier.Training();
            classifier.SaveToFile(fileInfo);
        }
        Classifier = classifier;
    }

    static async Task<int> RunServerAsync(Settings settings)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console(LogEventLevel.Verbose,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        WebServerLog.LogPreAdded += WebServerLog_LogPreAdded;

        using var server = new MaxLib.WebServer.Server(new WebServerSettings(settings.Port, 5000));
        server.InitialDefault();
        var initialServices = GetServices(server);

        static void SetupServer(Settings settings, MaxLib.WebServer.Server server, List<WebService> initialServices, bool fromUpdate)
        {
            if (fromUpdate)
            {
                Log.Information("Application updated, clearing old services...");
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
                Log.Information("Application updated, starting new services...");
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
                Log.Information("Data directory '{dir}' added at request path '/data'.",
                    settings.DataDirectory.FullName);
            }

            // add rest api service
            var restApi = MaxLib.WebServer.Builder.Service.Build<RestApi>();
            if(restApi is not null)
            {
                server.AddWebService(restApi);
                Log.Information("Rest API service added at request path '/api'.");
            }

            if (fromUpdate)
                Log.Information("Application update completed.");

            Task.Run(async () => await SetupBrain(settings));
        }

        if (settings.CacheDirectory != null)
        {
            await MimeType.LoadMimeTypesForExtensions(true, Path.Combine(settings.CacheDirectory.FullName, "mime-types.json"));
        }
        else
        {
            await MimeType.LoadMimeTypesForExtensions(false, null);
        }

        SetupServer(settings, server, initialServices, false);
        ApplicationUpdated += () => SetupServer(settings, server, initialServices, true);

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
                new LogEventProperty("info", new ScalarValue(eventArgs.LogItem.Information))
            ]
        ));
    }
}
