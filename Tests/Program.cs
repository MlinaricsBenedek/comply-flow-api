using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using comply_flow_api.Models;
using comply_flow_api.Repositories;
using comply_flow_api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Generation payload and guidance", GenerationPayload),
    ("Transfer fee facts", TransferFeeFacts),
    ("Customer texts use valid Hungarian", CustomerTexts),
    ("Actual rule values are used", DifferentRule),
    ("Explicit model selection", ExplicitModel),
    ("Missing rule data fails", MissingRuleData),
    ("Duplicate sources", DuplicateSources),
    ("Python 422 validation error", ValidationError),
    ("RAG pipeline and retrieved sources", RagPipeline),
    ("RAG errors are persisted", RagFailure),
    ("Invalid RAG sources fail", RagInvalidSources)
};
var failures = 0;
foreach (var (name, run) in tests)
{
    try
    {
        await run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {name}: {exception}");
    }
}
return failures == 0 ? 0 : 1;

static JsonObject LoadRule(string id)
{
    var documents = JsonNode.Parse(File.ReadAllText("OTP.JSON"))!.AsArray();
    return documents[0]!["rules"]!.AsArray()
        .Select(node => node!.AsObject())
        .Single(rule => (string?)rule["id"] == id)
        .DeepClone().AsObject();
}

static string RuleSet(JsonObject rule) => new JsonArray(new JsonObject
{
    ["ruleSetVersion"] = "test",
    ["rules"] = new JsonArray(rule)
}).ToJsonString();

static async Task GenerationPayload()
{
    var rule = LoadRule("HU-BANK-TRANSFER-FEES-001");
    using var fixture = new Fixture(RuleSet(rule), "BANK_TRANSFER_FEES");
    var processing = await fixture.Run();
    using var json = JsonDocument.Parse(fixture.Handler.GenerationBody!);
    var root = json.RootElement;
    Check(root.GetProperty("rule_decision").GetString() == "Rule Not Found", "insufficient data decision");
    Check(root.GetProperty("rule_reason").GetString()!.Contains("nem áll rendelkezésre elegendő adat"), "insufficient data reason");
    Check(root.GetProperty("matched_rules").EnumerateArray().Select(x => x.GetString())
        .SequenceEqual(new[] { "HU-BANK-TRANSFER-FEES-001" }), "selected rule");
    Check(root.GetProperty("input_data").GetProperty("decision").GetString() == "BankTransferFeesGuidance", "technical decision retained");
    Check(!root.TryGetProperty("model_name", out _), "default model must be omitted");
    Check(root.GetProperty("model_parameters").GetProperty("max_new_tokens").GetInt32() == 180, "model parameters");
    Check(!fixture.Handler.ModelsCalled, "default model should not require lookup");
    var text = root.GetProperty("rule_decision").GetString() + root.GetProperty("rule_reason").GetString();
    Check(!text.Contains("megalapozott", StringComparison.OrdinalIgnoreCase), "guidance must not decide complaint validity");
    Check(processing.Status == ProcessingStatus.Completed, "processing completed");
}

static async Task TransferFeeFacts()
{
    const string message = "OTP forint megtakarítási számla. Eseti, OTP-n belüli, nem saját számlára indított átutalás mobilbankon. " +
        "Az átutalás napja: 2026-07-01. Átutaltam 200000 Ft. A levont díj 1000 Ft.";
    foreach (var (charged, decision) in new[]
    {
        ("1000", "magasabb"),
        ("900", "megegyezik"),
        ("800", "alacsonyabb")
    })
    {
        using var fixture = new Fixture(RuleSet(LoadRule("HU-BANK-TRANSFER-FEES-001")), "BANK_TRANSFER_FEES");
        var processing = await fixture.Run(message.Replace("1000 Ft", charged + " Ft"));
        using var json = JsonDocument.Parse(fixture.Handler.GenerationBody!);
        var root = json.RootElement;
        Check(root.GetProperty("rule_decision").GetString()!.Contains(decision), $"comparison {charged}");
        var reason = root.GetProperty("rule_reason").GetString()!;
        Check(reason.Contains("900 Ft") && reason.Contains(charged + " Ft"), $"calculated fees {charged}");
        Check(processing.Response!.ResponseText.StartsWith("Válasz."), "LLM response replaced");
    }

    foreach (var invalidDate in new[] { "2026-06-30", "2026-02-31", "" })
    {
        using var fixture = new Fixture(RuleSet(LoadRule("HU-BANK-TRANSFER-FEES-001")), "BANK_TRANSFER_FEES");
        await fixture.Run(message.Replace("2026-07-01", invalidDate));
        using var json = JsonDocument.Parse(fixture.Handler.GenerationBody!);
        Check(json.RootElement.GetProperty("rule_decision").GetString() == "Rule Not Found", "unverified tariff date");
        Check(json.RootElement.GetProperty("rule_reason").GetString()!.Contains("dátuma"), "missing date not explained");
    }

    foreach (var invalidMessage in new[]
    {
        message.Replace("Eseti,", "Nem eseti,"),
        message.Replace("200000 Ft", "200100 Ft"),
        message.Replace("1000 Ft", "-1000 Ft"),
        message.Replace("A levont díj 1000 Ft.", "A levont díj 1000 Ft. Egy másik levont díj 1100 Ft.")
    })
    {
        using var fixture = new Fixture(RuleSet(LoadRule("HU-BANK-TRANSFER-FEES-001")), "BANK_TRANSFER_FEES");
        await fixture.Run(invalidMessage);
        using var json = JsonDocument.Parse(fixture.Handler.GenerationBody!);
        Check(json.RootElement.GetProperty("rule_decision").GetString() == "Rule Not Found", "ambiguous data produced a conclusion");
    }

    using var expectedFixture = new Fixture(RuleSet(LoadRule("HU-BANK-TRANSFER-FEES-001")), "BANK_TRANSFER_FEES");
    await expectedFixture.Run(message + " 500 Ft-ot vártam.");
    using var expectedJson = JsonDocument.Parse(expectedFixture.Handler.GenerationBody!);
    var expectedReason = expectedJson.RootElement.GetProperty("rule_reason").GetString()!;
    Check(expectedReason.Contains("900 Ft"), $"customer expectation replaced rule tariff: {expectedReason}");
}

static Task CustomerTexts()
{
    foreach (var file in new[] { "OTP.JSON", "Raiffeisen.JSON" })
    {
        var documents = JsonNode.Parse(File.ReadAllText(file))!.AsArray();
        foreach (var document in documents)
        foreach (var rule in document!["rules"]!.AsArray())
        foreach (var field in new[] { "customerDecision", "customerReason" })
        {
            var value = (string?)rule!["result"]![field];
            Check(!string.IsNullOrWhiteSpace(value) && !value.Contains('?'), $"{file}: invalid {field}");
        }
    }
    return Task.CompletedTask;
}

static async Task DifferentRule()
{
    var versions = JsonNode.Parse(File.ReadAllText("OTP.JSON"))!.AsArray();
    foreach (var version in versions)
    {
        var rule = version!["rules"]!.AsArray()
            .Select(node => node!.AsObject())
            .Single(item => (string?)item["id"] == "HU-OTP-CARD-DELIVERY-001")
            .DeepClone().AsObject();
        using var fixture = new Fixture(RuleSet(rule), "CARD_DELIVERY");
        var processing = await fixture.Run();
        using var json = JsonDocument.Parse(fixture.Handler.GenerationBody!);
        Check(json.RootElement.GetProperty("rule_decision").GetString() == (string?)rule["result"]!["customerDecision"], "other decision");
        Check(json.RootElement.GetProperty("rule_reason").GetString() == (string?)rule["result"]!["customerReason"], "other reason");
        Check(json.RootElement.GetProperty("matched_rules")[0].GetString() == (string?)rule["id"], "other rule ID");
        var response = processing.Response!.ResponseText;
        Check(response.Contains("Ha a kártyát bankfiókban") &&
            response.Contains("megrendeléstől számított két héten belül") &&
            response.Contains("nem ellenőrzött OTP-tájékoztatás"), "branch delivery assumption");
    }
}

static async Task ExplicitModel()
{
    using var fixture = new Fixture(RuleSet(LoadRule("HU-BANK-TRANSFER-FEES-001")), "BANK_TRANSFER_FEES", "model");
    await fixture.Run();
    using var json = JsonDocument.Parse(fixture.Handler.GenerationBody!);
    Check(fixture.Handler.ModelsCalled, "configured model not validated");
    Check(json.RootElement.GetProperty("model_name").GetString() == "model", "configured model not sent");
}

static async Task MissingRuleData()
{
    foreach (var missing in new[] { "customerDecision", "customerReason", "empty-decision", "empty-reason", "no-match" })
    {
        var rule = LoadRule("HU-BANK-TRANSFER-FEES-001");
        if (missing == "empty-decision")
            rule["result"]!["customerDecision"] = " ";
        else if (missing == "empty-reason")
            rule["result"]!["customerReason"] = " ";
        else if (missing != "no-match")
            rule["result"]!.AsObject().Remove(missing);
        using var fixture = new Fixture(RuleSet(rule), missing == "no-match" ? "UNMATCHED" : "BANK_TRANSFER_FEES");
        await Throws<InvalidDataException>(() => fixture.Run());
        Check(fixture.Handler.GenerationBody is null, $"{missing} sent a request");
        Check(fixture.Repository.Failed, $"{missing} not marked failed");
    }
}

static async Task DuplicateSources()
{
    var rule = LoadRule("HU-BANK-TRANSFER-FEES-001");
    rule["source"] = "Ellenőrzött hivatkozás";
    rule["effectiveFrom"] = " ellenőrzött hivatkozás ";
    using var fixture = new Fixture(RuleSet(rule), "BANK_TRANSFER_FEES");
    var processing = await fixture.Run();
    var sources = JsonSerializer.Deserialize<List<string>>(processing.ResponsePlan!.SourcesJson)!;
    Check(sources.Count == 1, "duplicate response-plan sources");
    Check(processing.Response!.ResponseText.Split("Ellenőrzött hivatkozás").Length == 2, "duplicate LLM citations");

    var templates = new ResponseTemplateSet { TemplateVersion = "test", Templates = new() { ["default"] = "{{sources}}" } };
    var rendered = TemplateResponseRenderer.Render(templates, "x", "x", "x", "[]", "{}", "[\"Forrás\", \" forrás \"]");
    Check(rendered.Split(Environment.NewLine).Length == 1, "duplicate template citations");
}

static async Task ValidationError()
{
    using var fixture = new Fixture(RuleSet(LoadRule("HU-BANK-TRANSFER-FEES-001")), "BANK_TRANSFER_FEES");
    fixture.Handler.FailGeneration = true;
    var exception = await Throws<AiServiceException>(() => fixture.Run());
    Check(exception.StatusCode == HttpStatusCode.UnprocessableEntity, "status");
    Check(exception.FailureKind == AiServiceFailureKind.ConfigurationError, "failure kind");
    Check(exception.Message.Contains("body.rule_reason"), "field location");
    Check(!exception.Message.Contains("secret-value"), "input value leaked");
    Check(exception.CorrelationId == "trace-422", "correlation");
    Check(fixture.Repository.Failed, "failure persisted");
}

static async Task RagPipeline()
{
    using var fixture = new Fixture("invalid rules", "BANK_TRANSFER_FEES", mode: "Rag");
    var processing = await fixture.Run();
    using var request = JsonDocument.Parse(fixture.Handler.GenerationBody!);
    var root = request.RootElement;
    Check(fixture.Handler.GenerationPath == "/generate-rag", "wrong endpoint");
    Check(root.GetProperty("message").GetString() == "Tesztüzenet", "preprocessed message");
    Check(root.GetProperty("bank").GetString() == "OTP", "bank spelling");
    Check(root.GetProperty("facts").ValueKind == JsonValueKind.Object, "facts missing");
    Check(root.GetProperty("category").GetString() == "BANK_TRANSFER_FEES", "category missing");
    Check(root.GetProperty("retrieval_top_k").GetInt32() == 3, "top k");
    Check(root.GetProperty("retrieval_min_similarity").GetDouble() == 0.4, "similarity");
    Check(!root.TryGetProperty("input_data", out _), "legacy payload leaked");
    Check(processing.Status == ProcessingStatus.Completed && processing.Rules is null && processing.ResponsePlan is null,
        "RAG must not use the rule pipeline");
    Check(processing.Response!.ResponseText == "RAG válasz.", "response");
    var result = await fixture.GetResult();
    Check(result!.Results!.Response!.Sources!.Single().DocumentName == "Hirdetmény", "stored sources");
    Check(result.Results.EvaluationResults.Single().Score == 0.8m, "similarity metric");
}

static async Task RagFailure()
{
    using var fixture = new Fixture("invalid rules", "BANK_TRANSFER_FEES", mode: "Rag");
    fixture.Handler.FailGeneration = true;
    var exception = await Throws<AiServiceException>(() => fixture.Run());
    Check(exception.Endpoint == "generate-rag", "error endpoint");
    Check(fixture.Repository.Failed, "failure not persisted");
}

static async Task RagInvalidSources()
{
    using var fixture = new Fixture("invalid rules", "BANK_TRANSFER_FEES", mode: "Rag");
    fixture.Handler.InvalidRagSources = true;
    var exception = await Throws<AiServiceException>(() => fixture.Run());
    Check(exception.FailureKind == AiServiceFailureKind.InvalidResponse, "invalid response not detected");
    Check(fixture.Repository.Failed, "failure not persisted");
}

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static async Task<T> Throws<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T exception) { return exception; }
    throw new Exception($"Expected {typeof(T).Name}");
}

sealed class Fixture : IDisposable
{
    private readonly string _logPath = Path.GetTempFileName();
    private readonly ProcessingService _service;
    public FakeHandler Handler { get; }
    public FakeProcessingRepository Repository { get; } = new();

    public Fixture(string rules, string category, string? modelName = null, string mode = "Llm")
    {
        Handler = new FakeHandler(category);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RequestLogging:FilePath"] = _logPath
        }).Build();
        var logger = new PayloadFileLogger(new TestEnvironment(), config, NullLogger<PayloadFileLogger>.Instance);
        var client = new AiServiceClient(new HttpClient(Handler) { BaseAddress = new Uri("http://localhost/") },
            Options.Create(new AiServiceOptions()), logger, new HttpContextAccessor());
        _service = new ProcessingService(new FakeConfigurationRepository(modelName, mode), Repository, client,
            new FakeTextFileService(rules), NullLogger<ProcessingService>.Instance);
    }

    public async Task<Processing> Run(string message = "Tesztüzenet") =>
        (await _service.CreateForMessageAsync(new Message { ConversationId = 1, Content = message }, 1, CancellationToken.None)).Processing!;

    public Task<comply_flow_api.Models.dtos.ProcessDtos.ProcessingResultResponse?> GetResult() =>
        _service.GetResultAsync(1, CancellationToken.None);

    public void Dispose() => File.Delete(_logPath);
}

sealed class FakeHandler(string category) : HttpMessageHandler
{
    public string? GenerationBody { get; private set; }
    public string? GenerationPath { get; private set; }
    public bool ModelsCalled { get; private set; }
    public bool FailGeneration { get; set; }
    public bool InvalidRagSources { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path == "/preprocessing")
            return Json(HttpStatusCode.OK, JsonSerializer.Serialize(new { message = "Tesztüzenet", facts = new Dictionary<string, string[]>(), category, success = true }));
        if (path == "/models")
        {
            ModelsCalled = true;
            return Json(HttpStatusCode.OK, "{\"success\":true,\"models\":[{\"name\":\"model\",\"is_default\":true}]}");
        }
        if (path is not ("/generate" or "/generate-rag")) throw new Exception($"Unexpected endpoint {path}");
        GenerationPath = path;
        GenerationBody = await request.Content!.ReadAsStringAsync(cancellationToken);
        if (path == "/generate-rag" && InvalidRagSources)
            return Json(HttpStatusCode.OK, "{\"success\":true,\"response\":\"Válasz\",\"prompt_version\":\"rag-v1\",\"model_parameters\":{},\"sources\":[{\"source_id\":\"s1\"}]}");
        if (path == "/generate-rag" && !FailGeneration)
            return Json(HttpStatusCode.OK, "{\"success\":true,\"response\":\"RAG válasz.\",\"prompt_version\":\"rag-v1\",\"model_name\":null,\"model_id\":null,\"model_parameters\":{},\"reference_similarity\":0.8,\"reference_similarity_metric\":\"token_f1\",\"sources\":[{\"source_id\":\"s1\",\"document_name\":\"Hirdetmény\",\"chunk_index\":0,\"text\":\"részlet\",\"start_char\":0,\"end_char\":7,\"similarity\":0.9}]}");
        if (!FailGeneration) return Json(HttpStatusCode.OK, "{\"success\":true,\"response\":\"Válasz.\"}");
        var response = Json(HttpStatusCode.UnprocessableEntity, "{\"detail\":[{\"loc\":[\"body\",\"rule_reason\"],\"msg\":\"invalid\",\"input\":\"secret-value\"}]}");
        response.Headers.Add("x-correlation-id", "trace-422");
        return response;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}

sealed class FakeConfigurationRepository(string? modelName, string mode) : IConfigurationRepository
{
    public Task<comply_flow_api.Models.Configuration?> GetByIdAsync(int id, CancellationToken token) =>
        Task.FromResult<comply_flow_api.Models.Configuration?>(new() {
            Bank = "otp", GenerationMode = mode,
            ModelName = modelName,
            RetrievalTopK = mode == "Rag" ? 3 : null,
            RetrievalMinSimilarity = mode == "Rag" ? 0.4 : null,
            ModelParametersJson = "{\"max_new_tokens\":180,\"do_sample\":false}"
        });
    public Task AddAsync(comply_flow_api.Models.Configuration configuration, CancellationToken token) => throw new NotSupportedException();
    public Task<List<comply_flow_api.Models.Configuration>> GetAllAsync(CancellationToken token) => throw new NotSupportedException();
}

sealed class FakeProcessingRepository : IProcessingRepository
{
    public bool Failed { get; private set; }
    private Processing? _processing;
    public Task AddAsync(Processing processing, CancellationToken token) { _processing = processing; return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken token) => Task.CompletedTask;
    public Task<bool> MarkFailedAsync(int id, string type, string message, DateTime finishedAt, CancellationToken token)
    {
        Failed = true;
        return Task.FromResult(true);
    }
    public Task<Processing?> GetByIdAsync(int id, CancellationToken token) => throw new NotSupportedException();
    public Task<Processing?> GetResultByIdAsync(int id, CancellationToken token) => Task.FromResult(_processing);
}

sealed class FakeTextFileService(string rules) : ITextFileService
{
    public Task<string> GetBusinessRulesAsync(string bank, CancellationToken token = default) => Task.FromResult(rules);
    public Task<string> GetPromptAsync(CancellationToken token = default) => throw new NotSupportedException();
    public Task<ResponseTemplateSet> GetResponseTemplateSetAsync(string version, CancellationToken token = default) => throw new NotSupportedException();
}

sealed class TestEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "Tests";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = "";
    public string EnvironmentName { get; set; } = "Development";
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
