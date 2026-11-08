using System.Collections.Concurrent;

// Fake SMS provider (our own contract, an assumption since the exam's provider was not supplied):
//   POST /send               { to, body }  -> 202 { providerMessageId }
//   GET  /status/{id}                      -> 200 { status: "Sent" | "Delivered" | "Failed" } or 404
// Deterministic behaviour: "Sent" right away, "Delivered" after 3 seconds.
// A number ending with "0000" is always "Failed" (to demo failures without configuration).

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var messages = new ConcurrentDictionary<string, FakeMessage>();
var deliveryDelay = TimeSpan.FromSeconds(3);

app.MapPost("/send", (SendRequest request, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(request.To) || string.IsNullOrWhiteSpace(request.Body))
        return Results.BadRequest(new { error = "to and body are required" });

    var id = Guid.NewGuid().ToString("N");
    messages[id] = new FakeMessage(request.To, request.Body, DateTime.UtcNow);
    logger.LogInformation("Accepted {Id} to {To}: {Body}", id, request.To, request.Body);
    return Results.Accepted($"/status/{id}", new { providerMessageId = id });
});

app.MapGet("/status/{id}", (string id) =>
{
    if (!messages.TryGetValue(id, out var message))
        return Results.NotFound();

    string status;
    if (DateTime.UtcNow - message.AcceptedAt < deliveryDelay)
        status = "Sent";
    else
        status = message.To.EndsWith("0000") ? "Failed" : "Delivered";

    return Results.Ok(new { status });
});

app.MapGet("/", () => "Fake SMS provider is running");

app.Run();

record SendRequest(string? To, string? Body);
record FakeMessage(string To, string Body, DateTime AcceptedAt);
