using MediatR;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> log)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        log.LogInformation("Handling {Request}", typeof(TRequest).Name);
        var response = await next();
        log.LogInformation("Handled {Response}", typeof(TResponse).Name);
        return response;
    }
}
