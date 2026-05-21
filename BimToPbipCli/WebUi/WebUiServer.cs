using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace BimToPbipCli.WebUi;

/// <summary>
/// Serves a small local web UI for the converter. The executable starts an
/// HTTP listener bound to loopback only, opens the default browser at it, and
/// handles convert / file-pick / open-folder requests.
///
/// Binding to "http://localhost:&lt;port&gt;/" works for a non-admin user on
/// Windows without any URL-ACL setup.
/// </summary>
public static class WebUiServer
{
    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static ExitCode Launch()
    {
        var port = FindFreePort();
        var url = $"http://localhost:{port}/";

        var listener = new HttpListener();
        listener.Prefixes.Add(url);
        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            Console.Error.WriteLine($"[ ERROR ] Could not start the web UI listener on {url}: {ex.Message}");
            return ExitCode.UnexpectedError;
        }

        Console.WriteLine("BimToPbipCli — web UI");
        Console.WriteLine($"  Open in browser: {url}");
        Console.WriteLine("  Close this window to stop the converter.");
        OpenBrowser(url);

        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = listener.GetContext();
            }
            catch (Exception)
            {
                break; // listener stopped
            }

            try
            {
                HandleRequest(context);
            }
            catch (Exception ex)
            {
                TryWriteError(context, ex);
            }
        }

        return ExitCode.Success;
    }

    private static void HandleRequest(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";

        switch (path)
        {
            case "/":
                WriteText(context.Response, IndexPage.Html, "text/html; charset=utf-8");
                break;

            case "/api/pick":
                HandlePick(context);
                break;

            case "/api/convert":
                HandleConvert(context);
                break;

            case "/api/open":
                HandleOpen(context);
                break;

            default:
                WriteText(context.Response, "Not found", "text/plain; charset=utf-8", 404);
                break;
        }
    }

    private static void HandlePick(HttpListenerContext context)
    {
        var request = ReadJson<PickRequest>(context.Request);
        var kind = request?.Type?.ToLowerInvariant() switch
        {
            "bim" => NativeFilePicker.PickKind.BimFile,
            "pbitools" => NativeFilePicker.PickKind.PbiToolsExecutable,
            "folder" => NativeFilePicker.PickKind.Folder,
            _ => (NativeFilePicker.PickKind?)null,
        };

        var picked = kind is null ? null : NativeFilePicker.Pick(kind.Value);
        WriteJson(context.Response, new { path = picked });
    }

    private static void HandleConvert(HttpListenerContext context)
    {
        var request = ReadJson<ConvertRequest>(context.Request);

        if (request is null || string.IsNullOrWhiteSpace(request.Bim))
        {
            WriteJson(context.Response, new
            {
                success = false,
                exitCode = (int)ExitCode.InvalidArguments,
                projectPath = (string?)null,
                message = "No model.bim file was provided.",
                log = new[] { new LogEntry("error", "No model.bim file was provided.") },
            });
            return;
        }

        var options = new CliOptions
        {
            BimPath = request.Bim,
            OutputRoot = NullIfBlank(request.Out),
            DatasetName = NullIfBlank(request.Dataset),
            PbiToolsPath = NullIfBlank(request.PbiToolsPath),
            KeepTemp = request.KeepTemp,
        };

        var logger = new CapturingLogger();
        var result = new ConversionService(logger).Run(options);

        WriteJson(context.Response, new
        {
            success = result.Succeeded,
            exitCode = (int)result.ExitCode,
            projectPath = result.PbipProjectPath,
            message = result.Message,
            log = logger.Entries,
        });
    }

    private static void HandleOpen(HttpListenerContext context)
    {
        var request = ReadJson<OpenRequest>(context.Request);
        var ok = false;

        if (!string.IsNullOrWhiteSpace(request?.Path) && Directory.Exists(request.Path))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = request.Path,
                    UseShellExecute = true,
                });
                ok = true;
            }
            catch
            {
                ok = false;
            }
        }

        WriteJson(context.Response, new { ok });
    }

    // ----- helpers ------------------------------------------------------------------------

    private static int FindFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            Console.WriteLine("  (Could not open the browser automatically — open the URL above manually.)");
        }
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static T? ReadJson<T>(HttpListenerRequest request) where T : class
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        var body = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(body, JsonReadOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void WriteJson(HttpListenerResponse response, object payload)
    {
        WriteText(response, JsonSerializer.Serialize(payload, JsonWriteOptions),
            "application/json; charset=utf-8");
    }

    private static void WriteText(HttpListenerResponse response, string content, string contentType, int status = 200)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        response.StatusCode = status;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes, 0, bytes.Length);
        response.OutputStream.Close();
    }

    private static void TryWriteError(HttpListenerContext context, Exception ex)
    {
        try
        {
            WriteText(context.Response, $"Internal error: {ex.Message}",
                "text/plain; charset=utf-8", 500);
        }
        catch
        {
            // The response may already be (partly) sent — nothing more to do.
        }
    }

    private sealed class PickRequest
    {
        public string? Type { get; set; }
    }

    private sealed class ConvertRequest
    {
        public string Bim { get; set; } = string.Empty;
        public string? Out { get; set; }
        public string? Dataset { get; set; }
        public string? PbiToolsPath { get; set; }
        public bool KeepTemp { get; set; }
    }

    private sealed class OpenRequest
    {
        public string? Path { get; set; }
    }
}
