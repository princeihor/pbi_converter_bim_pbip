using BimToPbipCli;

// Entry point. Responsibilities are kept thin: parse arguments, delegate to the
// conversion service, map the outcome to a process exit code.

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
