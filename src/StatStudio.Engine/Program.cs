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
    try
    {
        var req = JsonSerializer.Deserialize<EngineRequest>(line, EngineJson.Options)
                  ?? throw new Exception("null request");
        res = Dispatcher.Handle(req);
    }
    catch (Exception ex)
    {
        res = new EngineResponse { Id = 0, Ok = false, Error = "parse error: " + ex.Message };
    }

    stdout.WriteLine(JsonSerializer.Serialize(res, EngineJson.Options));
    stdout.Flush();
}
