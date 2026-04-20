using GameOfLife.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GameOfLife.API.ExceptionHandlers;

public sealed class BoardDidNotStabilizeExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not BoardDidNotStabilizeException ex)
            return false;

        var problemDetails = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            Title = "Board Did Not Stabilize",
            Status = StatusCodes.Status422UnprocessableEntity,
            Detail = ex.Message,
            Instance = httpContext.Request.Path
        };
        problemDetails.Extensions["boardId"] = ex.BoardId;
        problemDetails.Extensions["maxIterations"] = ex.MaxIterations;

        httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}