using BimToPbipCli;
using BimToPbipCli.WebUi;

// Entry point. Responsibilities are kept thin: pick the mode, delegate, and
// map the outcome to a process exit code.

// No arguments (e.g. double-clicked in Explorer) or an explicit --ui flag
// launches the browser-based UI. Otherwise run as a classic CLI.
var wantsUi = args.Length == 0
    || Array.Exists(args, a => string.Equals(a, "--ui", StringComparison.OrdinalIgnoreCase));

if (wantsUi)
{
    return (int)WebUiServer.Launch();
}

var log = new ConsoleLogger();

CliOptions options;
try
{
    options = CliParser.Parse(args);
}
catch (CliParseException ex)
{
    log.Error(ex.Message);
    Console.Error.WriteLine();
    Console.Error.WriteLine(CliParser.HelpText);
    return (int)ExitCode.InvalidArguments;
}

if (options.ShowHelp)
{
    Console.Out.WriteLine(CliParser.HelpText);
    return (int)ExitCode.Success;
}

log.Info("BimToPbipCli — BIM -> PBIP project converter");

var service = new ConversionService(log);
var result = service.Run(options);

return (int)result.ExitCode;
