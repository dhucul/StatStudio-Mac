using System.Text.Json;
using StatStudio.Engine;

// StatStudio.Engine — the headless .NET helper that backs the native macOS app.
// Protocol: newline-delimited JSON. One request object per line on stdin, one response
// object per line on stdout. Binary (graph PNGs) travels as base64. The process stays
// alive for the whole session; the Swift EngineClient correlates responses by `id`.

var stdout = Console.Out;

string? line;
while ((line = Console.In.ReadLine()) is not null)
{
    if (string.IsNullOrWhiteSpace(line)) continue;

    EngineResponse res;
    // Recover the id before the typed parse: the client correlates strictly by id, so a
    // parse failure reported against id 0 reads to it as a protocol desync rather than
    // the error it actually is.
    int requestId = 0;
    try
    {
        using (var probe = JsonDocument.Parse(line))
        {
            if (probe.RootElement.ValueKind == JsonValueKind.Object &&
                probe.RootElement.TryGetProperty("id", out var idElement) &&
                idElement.ValueKind == JsonValueKind.Number &&
                idElement.TryGetInt32(out var parsedId))
                requestId = parsedId;
        }
    }
    catch (JsonException) { /* unparseable line: fall back to id 0 */ }

    try
    {
        var req = JsonSerializer.Deserialize<EngineRequest>(line, EngineJson.Options)
                  ?? throw new Exception("null request");
        res = Dispatcher.Handle(req);
    }
    catch (Exception ex)
    {
        res = new EngineResponse { Id = requestId, Ok = false, Error = "parse error: " + ex.Message };
    }

    stdout.WriteLine(JsonSerializer.Serialize(res, EngineJson.Options));
    stdout.Flush();
}
