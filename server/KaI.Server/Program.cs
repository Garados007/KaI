using MaxLib.WebServer;
using Serilog;
using Serilog.Events;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace KaI.Server;

class Settings
{
    public int Port { get; set; } = 8005;

    public DirectoryInfo? DataDirectory { get; set; }

    public DirectoryInfo? CacheDirectory { get; set; }
};

class Program
{
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
        if(settings.CacheDirectory != null)
        {
            await MimeType.LoadMimeTypesForExtensions(true, Path.Combine(settings.CacheDirectory.FullName, "mime-types.json"));
        }
        else
        {
            await MimeType.LoadMimeTypesForExtensions(false, null);
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
