using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;

namespace MedicalAssistant.AcceptanceTests.Contracts;

public sealed class BackendOpenApiContractTests : IClassFixture<BackendContractApiFactory>
{
    private readonly BackendContractApiFactory _factory;

    public BackendOpenApiContractTests(BackendContractApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OpenApi_document_matches_the_committed_backend_contract()
    {
        using var client = _factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

        var document = await client.GetStringAsync("/swagger/v1/swagger.json");
        var actual = CreateSnapshot(document);
        var snapshotPath = Path.Combine(
            AppContext.BaseDirectory,
            "Contracts",
            "Snapshots",
            "backend-v1.openapi.snapshot.json");
        var expected = Canonicalize(await File.ReadAllTextAsync(snapshotPath));

        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            var actualPath = Path.Combine(
                Path.GetTempPath(),
                "medicalassistant-backend-v1.openapi.actual.json");
            await File.WriteAllTextAsync(actualPath, actual);
            Assert.Fail($"Backend OpenAPI changed. Review the generated contract at {actualPath}.");
        }
    }

    private static string CreateSnapshot(string document)
    {
        var root = JsonNode.Parse(document)?.AsObject()
            ?? throw new InvalidOperationException("OpenAPI JSON was empty.");
        var operations = new JsonArray();
        foreach (var path in root["paths"]!.AsObject().OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            foreach (var operation in path.Value!.AsObject().OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                var body = operation.Value!.AsObject();
                operations.Add($"{operation.Key.ToUpperInvariant()} {path.Key} {Hash(body)}");
            }
        }

        var schemas = new JsonArray();
        foreach (var schema in root["components"]!["schemas"]!.AsObject()
                     .OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var body = schema.Value!.AsObject();
            schemas.Add($"{schema.Key} {Hash(body)}");
        }

        var snapshot = new JsonObject
        {
            ["info"] = root["info"]!.DeepClone(),
            ["openApiSha256"] = Hash(root),
            ["securitySchemes"] = root["components"]!["securitySchemes"]!.DeepClone(),
            ["operations"] = operations,
            ["schemas"] = schemas,
        };
        return Canonicalize(snapshot.ToJsonString());
    }

    private static string Hash(JsonNode node)
    {
        var canonical = Canonicalize(node.ToJsonString());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string Canonicalize(string json)
    {
        var node = JsonNode.Parse(json) ?? throw new InvalidOperationException("OpenAPI JSON was empty.");
        return Sort(node).ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }

    private static JsonNode Sort(JsonNode node) => node switch
    {
        JsonObject source => new JsonObject(
            source
                .OrderBy(property => property.Key, StringComparer.Ordinal)
                .Select(property => KeyValuePair.Create(
                    property.Key,
                    property.Value is null ? null : Sort(property.Value)))),
        JsonArray source => new JsonArray(
            source.Select(item => item is null ? null : Sort(item)).ToArray()),
        _ => node.DeepClone(),
    };
}
