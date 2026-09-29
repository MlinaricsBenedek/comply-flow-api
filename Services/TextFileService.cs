namespace comply_flow_api.Services;

public interface ITextFileService
{
    Task<string> GetPromptAsync(CancellationToken cancellationToken = default);
    Task<string> GetBusinessRulesAsync(CancellationToken cancellationToken = default);
}

public class TextFileService : ITextFileService
{
    private readonly string _promptDirectory;
    private readonly string _businessRulesDirectory;

    public TextFileService(IConfiguration configuration)
    {
        _promptDirectory = configuration["TextFiles:PromptDirectory"]
            ?? throw new InvalidOperationException("TextFiles:PromptDirectory is not configured.");
        _businessRulesDirectory = configuration["TextFiles:BusinessRulesDirectory"]
            ?? throw new InvalidOperationException("TextFiles:BusinessRulesDirectory is not configured.");
    }

    public Task<string> GetPromptAsync(CancellationToken cancellationToken = default) =>
        ReadTxtFilesAsync(_promptDirectory, cancellationToken);

    public Task<string> GetBusinessRulesAsync(CancellationToken cancellationToken = default) =>
        ReadBusinessRulesAsync(_businessRulesDirectory, cancellationToken);

    private static async Task<string> ReadBusinessRulesAsync(
        string directory,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Business rules directory was not found: {directory}");
        }

        var filePath = Path.Combine(directory, "Rules.JSON");
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