using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Api.Middleware;

public class IdempotencyFilterMiddleware : IAsyncActionFilter
{
    private readonly ICacheService _cacheService;
    private const string HeaderName = "Idempotency-Key";

    public IdempotencyFilterMiddleware(ICacheService cache)
    {
        _cacheService = cache;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var idempotencyKey))
        {
            throw new BadRequestException($"O header '{HeaderName}' é obrigatório para requisições idempotentes.");
        }

        string cacheKey = $"idempotency:{idempotencyKey}";

        var cachedResponse = await _cacheService.GetAsync<IdempotentResponseModel>(cacheKey);
        if (cachedResponse != null)
        {
            context.Result = new ObjectResult(cachedResponse.Body)
            {
                StatusCode = cachedResponse.StatusCode
            };

            return;
        }

        var executedContext = await next();

        if (executedContext.Result is ObjectResult objectResult)
        {
            var responseModel = new IdempotentResponseModel
            {
                StatusCode = objectResult.StatusCode ?? 200,
                Body = objectResult.Value
            };

            var serializedResponse = JsonSerializer.Serialize(responseModel);

            await _cacheService.SetAsync(cacheKey, serializedResponse, TimeSpan.FromMinutes(6));
        }
    }
}

public class IdempotentResponseModel
{
    public int StatusCode { get; set; }

    public object? Body { get; set; }
}
