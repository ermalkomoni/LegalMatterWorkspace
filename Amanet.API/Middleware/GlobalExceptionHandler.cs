using Amanet.Application.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Amanet.API.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        ProblemDetails problem = exception switch
        {
            ValidationException validation => new ValidationProblemDetails(validation.Errors
                .GroupBy(failure => failure.PropertyName)
                .ToDictionary(group => group.Key,
                    group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred."
            },
            ResourceNotFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound, Title = "Resource not found."
            },
            ConflictException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict, Title = "The request conflicts with the current state."
            },
            UnauthorizedAccessException => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized, Title = "Unauthorized."
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred."
            }
        };

        if (problem.Status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled request failure. TraceId: {TraceId}", httpContext.TraceIdentifier);
        }

        problem.Type = $"https://httpstatuses.io/{problem.Status}";
        problem.Instance = httpContext.Request.Path;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        httpContext.Response.StatusCode = problem.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null,
            contentType: "application/problem+json", cancellationToken: cancellationToken);
        return true;
    }
}
