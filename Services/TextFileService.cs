namespace comply_flow_api.Services;

public interface ITextFileService
{
    Task<string> GetPromptAsync(CancellationToken cancellationToken = default);
    Task<string> GetBusinessRulesAsync(string bank, CancellationToken cancellationToken = default);
}

public class TextFileService : ITextFileService
{
    private readonly string _promptDirectory;
    private readonly string _businessRulesDirectory;

    public TextFileService(IConfiguration configuration, IHostEnvironment environment)
    {
        _promptDirectory = ResolveDirectory(
            configuration["TextFiles:PromptDirectory"],
            environment.ContentRootPath,
            "TextFiles:PromptDirectory");
        _businessRulesDirectory = ResolveDirectory(
            configuration["TextFiles:BusinessRulesDirectory"],
            environment.ContentRootPath,
            "TextFiles:BusinessRulesDirectory");
    }

    public Task<string> GetPromptAsync(CancellationToken cancellationToken = default) =>
        ReadTxtFilesAsync(_promptDirectory, cancellationToken);

    public Task<string> GetBusinessRulesAsync(
        string bank,
        CancellationToken cancellationToken = default)
    {
        var fileName = bank switch
        {
            "otp" => "OTP.JSON",
            "raiffeisen" => "Raiffeisen.JSON",
            _ => throw new ArgumentOutOfRangeException(
                nameof(bank),
                bank,
                "The configured bank is not supported.")
        };

        return ReadBusinessRulesAsync(_businessRulesDirectory, fileName, cancellationToken);
    }

    private static string ResolveDirectory(string? directory, string contentRootPath, string settingName)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException($"{settingName} is not configured.");
        }

        return Path.GetFullPath(
            Path.IsPathRooted(directory) ? directory : Path.Combine(contentRootPath, directory));
    }

    private static async Task<string> ReadBusinessRulesAsync(
        string directory,
        string fileName,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Business rules directory was not found: {directory}");
        }

        var filePath = Path.Combine(directory, fileName);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Business rules file was not found: {filePath}");
        }

        return await File.ReadAllTextAsync(filePath, cancellationToken);
    }

    private static async Task<string> ReadTxtFilesAsync(
        string directory,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Text file directory was not found: {directory}");
        }

        var filePaths = Directory.GetFiles(directory, "*.txt", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (filePaths.Length == 0)
        {
            throw new FileNotFoundException($"No TXT files were found in directory: {directory}");
        }

        var contents = new string[filePaths.Length];
        for (var index = 0; index < filePaths.Length; index++)
        {
            contents[index] = await File.ReadAllTextAsync(filePaths[index], cancellationToken);
        }

        return string.Join(Environment.NewLine + Environment.NewLine, contents);
    }
}