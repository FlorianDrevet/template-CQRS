using System.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace Web.Template.CQRS.Api.Errors;

public static class ErrorHandling
{
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder builder)
    {
        return builder.UseExceptionHandler(exceptionHandlerApp =>
            exceptionHandlerApp.Run(async context =>
            {
                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
                var logger = context.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("GlobalExceptionHandler");

                if (exception is ValidationException validationException)
                {
                    var errors = validationException.Errors
                        .GroupBy(error => error.PropertyName, StringComparer.Ordinal)
                        .ToDictionary(
                            group => group.Key,
                            group => group.Select(error => error.ErrorMessage).ToArray(),
                            StringComparer.Ordinal);

                    await Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "One or more validation errors occurred.",
                        extensions: new Dictionary<string, object?>
                        {
                            ["code"] = "VALIDATION",
                            ["traceId"] = traceId,
                            ["errors"] = errors
                        }).ExecuteAsync(context);

                    return;
                }

                logger.LogError(exception, "An unhandled exception occurred for trace {TraceId}.", traceId);

                await Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "An unexpected error occurred.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "INTERNAL",
                        ["traceId"] = traceId
                    }).ExecuteAsync(context);
            }));
    }
}
