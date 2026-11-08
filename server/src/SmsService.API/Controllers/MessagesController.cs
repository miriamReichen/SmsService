using Microsoft.AspNetCore.Mvc;
using SmsService.Application;
using SmsService.DTO;

namespace SmsService.API.Controllers;

[ApiController]
[Route("messages")]
public class MessagesController : ControllerBase
{
    private readonly MessageService _messages;

    public MessagesController(MessageService messages) => _messages = messages;

    /// <summary>Creates a Pending message. Sending happens in the background. Returns 202 with the message id.</summary>
    [HttpPost]
    [ProducesResponseType<CreateMessageResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateMessageRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        var result = await _messages.CreateAsync(request, idempotencyKey, ct);
        return AcceptedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<MessageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<MessageDto> Get(Guid id, CancellationToken ct) => _messages.GetAsync(id, ct);

    /// <summary>Newest first, up to 100. Optional ?status=Pending|Sent|Delivered|Failed.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MessageDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<IReadOnlyList<MessageDto>> List([FromQuery] string? status, CancellationToken ct) =>
        _messages.ListAsync(status, ct);
}
