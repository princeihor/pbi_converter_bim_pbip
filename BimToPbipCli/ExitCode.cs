namespace BimToPbipCli;

/// <summary>
/// Process exit codes. Returned to the OS so the utility can be chained in scripts.
/// </summary>
public enum ExitCode
{
    Success = 0,

    /// <summary>Command line arguments were missing or malformed.</summary>
    InvalidArguments = 1,

    /// <summary>The input model.bim file does not exist.</summary>
    BimNotFound = 2,

    /// <summary>The input model.bim is not a valid TMSL JSON model.</summary>
    BimInvalid = 3,

    /// <summary>The assembled PBIP project failed its structural validation.</summary>
    ValidationFailed = 4,

    /// <summary>Creating or writing files in the target directory failed.</summary>
    OutputWriteFailed = 5,

    /// <summary>An unexpected, unclassified error occurred.</summary>
    UnexpectedError = 99,
}
