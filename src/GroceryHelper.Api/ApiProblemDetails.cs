using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

internal static class ApiProblemDetails
{
    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services)
    {
        services.AddExceptionHandler<PrivacySafeExceptionHandler>();
        services.AddSingleton<IProblemDetailsWriter, ApiProblemDetailsWriter>();
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = CustomizeProblemDetails);
        return services;
    }

    public static WebApplication UseApiProblemDetails(this WebApplication app)
    {
        app.UseWhen(
            context => context.Request.Path.StartsWithSegments("/api"),
            api =>
            {
                api.UseMiddleware<ApiErrorResponseMiddleware>();
                api.UseExceptionHandler();
                api.UseStatusCodePages();
            });
        return app;
    }

    private static void CustomizeProblemDetails(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        problem.Status = context.HttpContext.Response.StatusCode;
        if (problem is HttpValidationProblemDetails)
        {
            problem.Type = "urn:grocery-helper:problem:validation-error";
            problem.Title = "Validation failed";
        }
        else
        {
            problem.Type = "about:blank";
            var title = ReasonPhrases.GetReasonPhrase(problem.Status.Value);
            problem.Title = string.IsNullOrEmpty(title) ? "HTTP error" : title;
        }

        problem.Extensions["traceId"] = GetTraceId(context.HttpContext);
    }

    internal static string GetTraceId(HttpContext context) =>
        Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;
}

internal sealed class ApiErrorResponseMiddleware(
    RequestDelegate next,
    IProblemDetailsService problemDetailsService)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var response = context.Response;
        var originalBody = response.Body;
        await using var bufferedBody = new MemoryStream();
        response.Body = bufferedBody;

        try
        {
            await next(context);
            if (ShouldReplaceResponse(context))
            {
                bufferedBody.SetLength(0);
                response.ContentLength = null;
                response.ContentType = null;
                await problemDetailsService.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = response.StatusCode,
                    },
                });
            }

            bufferedBody.Position = 0;
            await bufferedBody.CopyToAsync(originalBody, context.RequestAborted);
        }
        finally
        {
            response.Body = originalBody;
        }
    }

    private static bool ShouldReplaceResponse(HttpContext context) =>
        context.Response.StatusCode is >= 400 and <= 599 &&
        !HttpMethods.IsHead(context.Request.Method) &&
        !context.Items.ContainsKey(ApiProblemDetailsWriter.WrittenKey);
}

internal sealed class PrivacySafeExceptionHandler(
    ILogger<PrivacySafeExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = ApiProblemDetails.GetTraceId(httpContext);
        logger.LogError(
            "Unhandled API exception: {EventName} {ExceptionType} " +
            "{ExceptionStackTrace} {ExceptionChain} {TraceId}",
            "UnhandledApiException",
            exception.GetType().FullName,
            exception.StackTrace ?? string.Empty,
            GetExceptionChain(exception),
            traceId);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
            },
        });
        return true;
    }

    private static string GetExceptionChain(Exception exception)
    {
        var details = new StringBuilder();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            details.AppendLine(current.GetType().FullName);
            details.AppendLine(current.StackTrace);
        }

        return details.ToString();
    }
}

internal sealed class ApiProblemDetailsWriter(
    IOptions<ProblemDetailsOptions> problemDetailsOptions) : IProblemDetailsWriter
{
    internal static object WrittenKey { get; } = new();

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public bool CanWrite(ProblemDetailsContext context) =>
        context.HttpContext.Request.Path.StartsWithSegments("/api");

    public async ValueTask WriteAsync(ProblemDetailsContext context)
    {
        problemDetailsOptions.Value.CustomizeProblemDetails?.Invoke(context);
        context.HttpContext.Items[WrittenKey] = true;
        if (HttpMethods.IsHead(context.HttpContext.Request.Method))
        {
            return;
        }

        var response = context.HttpContext.Response;
        response.ContentType = "application/problem+json";
        await JsonSerializer.SerializeAsync(
            response.Body,
            context.ProblemDetails,
            context.ProblemDetails.GetType(),
            SerializerOptions,
            context.HttpContext.RequestAborted);
    }
}
