using GameOfLife.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GameOfLife.API.ExceptionHandlers;

public sealed class BoardNotFoundExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not BoardNotFoundException ex)
            return false;

        var problemDetails = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
            Title = "Board Not Found",
            Status = StatusCodes.Status404NotFound,
            Detail = ex.Message,
            Instance = httpContext.Request.Path
        };
        problemDetails.Extensions["boardId"] = ex.BoardId;

        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}