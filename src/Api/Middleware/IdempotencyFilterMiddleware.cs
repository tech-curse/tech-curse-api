using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Api.Middleware;

public class IdempotencyFilterMiddleware : IAsyncActionFilter
{
    private const string HeaderName = "Idempotency-Key";

    private static readonly TimeSpan Validade = TimeSpan.FromMinutes(6);

    private readonly ICacheService _cacheService;
    private readonly ICurrentUserService _currentUserService;
    private readonly JsonSerializerOptions _opcoesJson;

    public IdempotencyFilterMiddleware(ICacheService cache, ICurrentUserService currentUserService, IOptions<JsonOptions> opcoesJson)
    {
        _cacheService = cache;
        _currentUserService = currentUserService;
        _opcoesJson = opcoesJson.Value.JsonSerializerOptions;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var idempotencyKey))
        {
            throw new BadRequestException($"O header '{HeaderName}' é obrigatório para requisições idempotentes.");
        }

        var requisicao = context.HttpContext.Request;
        var usuario = _currentUserService.GetUserId() ?? "anonimo";
        var cacheKey = $"idempotency:{usuario}:{requisicao.Method}:{requisicao.Path}:{idempotencyKey}";

        var cachedResponse = await _cacheService.GetAsync<IdempotentResponseModel>(cacheKey);
        if (cachedResponse != null)
        {
            context.Result = new ContentResult
            {
                StatusCode = cachedResponse.StatusCode,
                Content = cachedResponse.Body,
                ContentType = "application/json; charset=utf-8"
            };

            return;
        }

        var executedContext = await next();

        if (executedContext.Exception is null && executedContext.Result is ObjectResult objectResult)
        {
            var responseModel = new IdempotentResponseModel
            {
                StatusCode = objectResult.StatusCode ?? StatusCodes.Status200OK,
                Body = JsonSerializer.Serialize(objectResult.Value, _opcoesJson)
            };

            await _cacheService.SetAsync(cacheKey, responseModel, Validade);
        }
    }
}

public class IdempotentResponseModel
{
    public int StatusCode { get; set; }

    public string Body { get; set; } = string.Empty;
}
