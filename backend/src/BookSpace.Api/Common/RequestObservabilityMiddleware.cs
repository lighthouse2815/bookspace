using System.Collections;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace BookSpace.Api.Common;

public sealed class RequestObservabilityMiddleware(
    RequestDelegate next,
    ILogger<RequestObservabilityMiddleware> logger)
{
    public const string CorrelationIdHeaderName = "X-Correlation-ID";
    public const int MaximumCorrelationIdLength = 128;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context.Request.Headers);
        context.TraceIdentifier = correlationId;
        context.Response.Headers[CorrelationIdHeaderName] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeaderName] = correlationId;
            return Task.CompletedTask;
        });

        var scope = new RequestLogScope(context, correlationId);
        using (logger.BeginScope(scope))
        {
            try
            {
                await next(context);
            }
            finally
            {
                scope.Stop();
                logger.LogInformation(
                    "HTTP {RequestMethod} {Route} responded {StatusCode} in {ElapsedMilliseconds:0.00} ms " +
                    "(CorrelationId: {CorrelationId}, UserId: {UserId})",
                    scope.RequestMethod,
                    scope.Route,
                    scope.StatusCode,
                    scope.ElapsedMilliseconds,
                    scope.CorrelationId,
                    scope.UserId);
            }
        }
    }

    internal static string ResolveCorrelationId(IHeaderDictionary headers)
    {
        if (headers.TryGetValue(CorrelationIdHeaderName, out var values) &&
            IsValidCorrelationId(values))
        {
            return values[0]!;
        }

        return Guid.NewGuid().ToString("N");
    }

    private static bool IsValidCorrelationId(StringValues values)
    {
        if (values.Count != 1)
        {
            return false;
        }

        var value = values[0];
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (value.Length > MaximumCorrelationIdLength)
        {
            return false;
        }

        return value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
    }

    private sealed class RequestLogScope(HttpContext context, string correlationId) :
        IReadOnlyList<KeyValuePair<string, object?>>
    {
        private readonly long _startedAt = Stopwatch.GetTimestamp();
        private TimeSpan? _elapsed;

        public string CorrelationId => correlationId;

        public string RequestMethod => context.Request.Method;

        public string RequestPath => context.Request.Path.HasValue
            ? context.Request.Path.Value!
            : "/";

        public string Route => context.GetEndpoint() is RouteEndpoint endpoint
            ? endpoint.RoutePattern.RawText ?? RequestPath
            : RequestPath;

        public int StatusCode => context.Response.StatusCode;

        public double ElapsedMilliseconds =>
            (_elapsed ?? Stopwatch.GetElapsedTime(_startedAt)).TotalMilliseconds;

        public string? UserId
        {
            get
            {
                if (context.User.Identity?.IsAuthenticated != true)
                {
                    return null;
                }

                var value = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                            context.User.FindFirstValue("sub");
                return Guid.TryParse(value, out var userId)
                    ? userId.ToString()
                    : null;
            }
        }

        public int Count => 8;

        public KeyValuePair<string, object?> this[int index] => index switch
        {
            0 => new("CorrelationId", correlationId),
            1 => new("RequestMethod", RequestMethod),
            2 => new("Route", Route),
            3 => new("RequestPath", RequestPath),
            4 => new("StatusCode", StatusCode),
            5 => new("ElapsedMilliseconds", ElapsedMilliseconds),
            6 => new("UserId", UserId),
            7 => new("{OriginalFormat}",
                "CorrelationId={CorrelationId}, RequestMethod={RequestMethod}, Route={Route}, " +
                "RequestPath={RequestPath}, StatusCode={StatusCode}, " +
                "ElapsedMilliseconds={ElapsedMilliseconds}, UserId={UserId}"),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

        public void Stop() => _elapsed ??= Stopwatch.GetElapsedTime(_startedAt);

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            for (var index = 0; index < Count; index++)
            {
                yield return this[index];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
