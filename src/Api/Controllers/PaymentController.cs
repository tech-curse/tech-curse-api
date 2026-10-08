using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TechCurse.Api.Middleware;
using TechCurse.Application.DTOs;
using TechCurse.Application.Features.Payments.Commands.CreatePayment;
using TechCurse.Application.Features.Payments.Commands.ProcessPayment;
using TechCurse.Application.Features.Payments.Commands.RefundPayment;
using TechCurse.Application.Features.Payments.Queries.GetPaymentById;
using TechCurse.Application.Features.Payments.Queries.GetPayments;
using TechCurse.Application.Features.Payments.Queries.GetPaymentsByEnrollmentId;
using TechCurse.Application.Features.Payments.Queries.GetPaymentsByStudentId;

namespace TechCurse.Api.Controllers;

[ApiController]
[Route("tech-curse/[controller]")]
[Consumes("application/json")]
[Produces("application/json")]
[Tags("Payments")]
public class PaymentController : ControllerBase
{
    private readonly IMediator _mediator;

    public PaymentController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(
        Summary = "Lista os pagamentos cadastrados (paginado).",
        Description = "**Acesso:** Requer role de Admin"
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Retorna a lista paginada.", typeof(PagedResultDto<PaymentOutputDto>))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Acesso negado.", typeof(ProblemDetails))]
    public async Task<IActionResult> GetAll([FromQuery] PaginationParamsDto searchParams)
    {
        var result = await _mediator.Send(new GetPaymentsQuery(searchParams));
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize]
    [SwaggerOperation(
        Summary = "Busca os detalhes de um pagamento específico pelo ID.",
        Description = "**Acesso:** Requer role de Admin ou o Próprio Student"
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Pagamento encontrado.", typeof(PaymentOutputDto))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Pagamento não encontrado.", typeof(ProblemDetails))]
    public async Task<IActionResult> Get(int id)
    {
        var result = await _mediator.Send(new GetPaymentByIdQuery(id));
        return Ok(result);
    }

    [HttpGet("student/{studentId}")]
    [Authorize]
    [SwaggerOperation(
        Summary = "Lista todos os pagamentos de um estudante.",
        Description = "**Acesso:** Requer role de Admin ou o Próprio Student"
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Retorna a lista paginada de pagamentos do estudante.", typeof(PagedResultDto<PaymentOutputDto>))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Acesso negado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Estudante não encontrado.", typeof(ProblemDetails))]
    public async Task<IActionResult> GetByStudent(int studentId, [FromQuery] PaginationParamsDto searchParams)
    {
        var result = await _mediator.Send(new GetPaymentsByStudentIdQuery(studentId, searchParams));
        return Ok(result);
    }

    [HttpGet("enrollment/{enrollmentId}")]
    [Authorize]
    [SwaggerOperation(
        Summary = "Lista os pagamentos de uma matrícula.",
        Description = "**Acesso:** Requer role de Admin ou o Próprio Student"
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Retorna a lista de pagamentos da matrícula.", typeof(IEnumerable<PaymentOutputDto>))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Acesso negado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Matrícula não encontrada.", typeof(ProblemDetails))]
    public async Task<IActionResult> GetByEnrollment(int enrollmentId)
    {
        var result = await _mediator.Send(new GetPaymentsByEnrollmentIdQuery(enrollmentId));
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [TypeFilter(typeof(IdempotencyFilterMiddleware))]
    [SwaggerOperation(
        Summary = "Cria um novo pagamento no sistema.",
        Description = "**Acesso:** Requer role de Admin"
    )]
    [SwaggerResponse(StatusCodes.Status201Created, "Pagamento criado com sucesso.", typeof(PaymentOutputDto))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Acesso negado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status422UnprocessableEntity, "Erro de validação.", typeof(ProblemDetails))]
    public async Task<IActionResult> Post([FromBody] CreatePaymentDto input)
    {
        var result = await _mediator.Send(new CreatePaymentCommand(input.EnrollmentId, input.Amount));
        return CreatedAtAction(nameof(Get), new { id = result.PaymentId }, result);
    }

    [HttpPost("process")]
    [Authorize(Roles = "Admin")]
    [TypeFilter(typeof(IdempotencyFilterMiddleware))]
    [SwaggerOperation(
        Summary = "Processa um pagamento (marca como pago).",
        Description = "**Acesso:** Requer role de Admin"
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Pagamento processado com sucesso.", typeof(ProcessPaymentOutputDto))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Acesso negado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Pagamento não encontrado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status422UnprocessableEntity, "Erro ao processar pagamento.", typeof(ProblemDetails))]
    public async Task<IActionResult> Process([FromHeader(Name = "Idempotency-Key")] string idempotencyKey, [FromBody] ProcessPaymentDto input)
    {
        var result = await _mediator.Send(new ProcessPaymentCommand(input.PaymentId, input.type, idempotencyKey));
        return Ok(result);
    }

    [HttpPost("refund")]
    [Authorize(Roles = "Admin")]
    [TypeFilter(typeof(IdempotencyFilterMiddleware))]
    [SwaggerOperation(
        Summary = "Estorna um pagamento (marca como reembolsado).",
        Description = "**Acesso:** Requer role de Admin"
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Pagamento estornado com sucesso.", typeof(RefundPaymentOutputDto))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Acesso negado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Pagamento não encontrado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status422UnprocessableEntity, "Erro ao estornar pagamento.", typeof(ProblemDetails))]
    public async Task<IActionResult> Refund([FromHeader(Name = "Idempotency-Key")] string idempotencyKey, [FromBody] RefundPaymentDto input)
    {
        var result = await _mediator.Send(new RefundPaymentCommand(input.PaymentId, idempotencyKey));
        return Ok(result);
    }
}
