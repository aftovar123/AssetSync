using System.Text.Json;
using AssetSync.Api;
using AssetSync.Application.Common.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssetSync.Tests;

public class GlobalExceptionHandlerTests
{
    private readonly GlobalExceptionHandler _handler = new(NullLogger<GlobalExceptionHandler>.Instance);

    private static DefaultHttpContext CreateContext(out MemoryStream body)
    {
        body = new MemoryStream();
        return new DefaultHttpContext { Response = { Body = body } };
    }

    [Fact]
    public async Task TryHandleAsync_ValidationException_Writes400WithFieldErrors()
    {
        var context = CreateContext(out var body);
        var exception = new ValidationException([new ValidationFailure("Code", "'Code' no debería estar vacío.")]);

        var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        var problem = await ReadBody<HttpValidationProblemDetails>(body);
        Assert.Contains("Code", problem.Errors.Keys);
    }

    [Fact]
    public async Task TryHandleAsync_NotFoundException_Writes404WithMessage()
    {
        var context = CreateContext(out var body);
        var exception = new NotFoundException("Work order 99 not found.");

        var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        var problem = await ReadBody<ProblemDetails>(body);
        Assert.Equal("Work order 99 not found.", problem.Title);
    }

    [Fact]
    public async Task TryHandleAsync_UnknownException_Writes500WithoutLeakingDetails()
    {
        var context = CreateContext(out var body);
        var exception = new InvalidOperationException("connection string has a password in it");

        var handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        var problem = await ReadBody<ProblemDetails>(body);
        Assert.DoesNotContain("password", problem.Title);
    }

    private static async Task<T> ReadBody<T>(MemoryStream body)
    {
        body.Seek(0, SeekOrigin.Begin);
        return (await JsonSerializer.DeserializeAsync<T>(body))!;
    }
}
