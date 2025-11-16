using System.CommandLine;
using Spelunk.Server.Modes;
using Spelunk.Server.Process;

namespace Spelunk.Server;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Spelunk.NET - MCP server for .NET code analysis");

        // stdio command
        var stdioCommand = new Command("stdio", "Run in stdio mode (JSON-RPC over standard input/output)");
        stdioCommand.SetHandler(async () =>
        {
            var mode = new StdioMode(args);
            await mode.RunAsync();
        });

        // sse command group
        var sseCommand = new Command("sse", "Run SSE server or manage background instance");

        var portOption = new Option<int>(
            aliases: new[] { "-p", "--port" },
            getDefaultValue: () => 3333,
            description: "Port for SSE server");

        var backgroundOption = new Option<bool>(
            name: "--background",
            description: "Internal flag - run as background process");
        backgroundOption.IsHidden = true; // Hidden from help

        // sse (no subcommand) - start in background or run as background process
        sseCommand.AddOption(portOption);
        sseCommand.AddOption(backgroundOption);
        sseCommand.SetHandler(async (int port, bool isBackground) =>
        {
            if (isBackground)
            {
                // We are the background process - run SSE server directly
                var mode = new SseMode(port, isBackgroundProcess: true);
                await mode.RunAsync();
            }
            else
            {
                // Start SSE server in background
                var result = await ProcessManager.StartSseServerAsync(port);
                Console.WriteLine(result.message);
                Environment.Exit(result.success ? 0 : 1);
            }
        }, portOption, backgroundOption);

        // sse stop
        var stopCommand = new Command("stop", "Stop background SSE server");
        stopCommand.SetHandler(() =>
        {
            var result = ProcessManager.StopSseServer();
            Console.WriteLine(result.message);
            Environment.Exit(result.success ? 0 : 1);
        });
        sseCommand.AddCommand(stopCommand);

        // sse status
        var statusCommand = new Command("status", "Check SSE server status");
        statusCommand.SetHandler(() =>
        {
            var result = ProcessManager.GetSseServerStatus();
            Console.WriteLine(result.message);
            Environment.Exit(result.running ? 0 : 1);
        });
        sseCommand.AddCommand(statusCommand);

        // sse restart
        var restartCommand = new Command("restart", "Restart background SSE server");
        restartCommand.AddOption(portOption);
        restartCommand.SetHandler(async (int port) =>
        {
            // If port is default (3333), use null to keep existing port
            int? newPort = port == 3333 ? null : port;
            var result = await ProcessManager.RestartSseServerAsync(newPort);
            Console.WriteLine(result.message);
            Environment.Exit(result.success ? 0 : 1);
        }, portOption);
        sseCommand.AddCommand(restartCommand);

        // sse logs
        var logsCommand = new Command("logs", "View SSE server logs");
        var followOption = new Option<bool>(
            aliases: new[] { "-f", "--follow" },
            description: "Follow log output");
        logsCommand.AddOption(followOption);
        logsCommand.SetHandler(async (bool follow) =>
        {
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };
            await ProcessManager.ShowLogsAsync(follow, cts.Token);
        }, followOption);
        sseCommand.AddCommand(logsCommand);

        rootCommand.AddCommand(stdioCommand);
        rootCommand.AddCommand(sseCommand);

        return await rootCommand.InvokeAsync(args);
    }
}
