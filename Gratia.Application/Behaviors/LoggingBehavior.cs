using System.Diagnostics;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Gratia.Application.Behaviors;

// Runs around every command and query.
// It logs only the message type, never the contents: messages carry passwords and tokens (e.g. LoginCommand).
// Failures aren't logged here: GlobalExceptionHandler logs unexpected errors, and 4xx errors are expected.
public sealed class LoggingBehavior<TMessage, TResponse>(ILogger<LoggingBehavior<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    private static readonly TimeSpan SlowThreshold = TimeSpan.FromMilliseconds(500);

    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var response = await next(message, cancellationToken);
        var elapsed = Stopwatch.GetElapsedTime(startedAt);

        if (elapsed > SlowThreshold)
            logger.LogWarning("Slow handler: {MessageName} took {ElapsedMilliseconds:0} ms", typeof(TMessage).Name, elapsed.TotalMilliseconds);
        else
            logger.LogInformation("Handled {MessageName} in {ElapsedMilliseconds:0} ms", typeof(TMessage).Name, elapsed.TotalMilliseconds);

        return response;
    }
}
