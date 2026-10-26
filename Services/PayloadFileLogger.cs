using System.Text;
using System.Text.Json;

namespace comply_flow_api.Services;

public sealed class PayloadFileLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private readonly string _filePath;
    private readonly ILogger<PayloadFileLogger> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public PayloadFileLogger(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<PayloadFileLogger> logger)
    {
        var configuredPath = configuration["RequestLogging:FilePath"]
            ?? Path.Combine("Logs", "api-payloads.jsonl");
        _filePath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);
        _logger = logger;
    }

    public async Task LogAsync(
        string direction,
        string method,
        string path,
        string requestId,
        string? body)
    {
        var entry = new PayloadLogEntry(
            DateTimeOffset.UtcNow,
            direction,
            method,
            path,
            requestId,
            body);
        var line = JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine;

        await _writeLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            await File.AppendAllTextAsync(_filePath, line, Utf8WithoutBom);
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "Could not write request payload log to {LogFilePath}.", _filePath);
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogError(exception, "Could not write request payload log to {LogFilePath}.", _filePath);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private sealed record PayloadLogEntry(
        DateTimeOffset Timestamp,
        string Direction,
        string Method,
        string Path,
        string RequestId,
        string? Body);
}
