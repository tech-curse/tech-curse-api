using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TechCurse.Application.DTOs;
using TechCurse.Application.Features.Enrollments.Commands.CreateEnrollment;

namespace TechCurse.Api.Controllers;

[ApiController]
[Route("tech-curse/[controller]")]
[Consumes("application/json")]
[Produces("application/json")]
[Tags("Enrollments")]
public class EnrollmentController : ControllerBase
{
    private readonly IMediator _mediator;

    public EnrollmentController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [Authorize]
    [SwaggerOperation(
        Summary = "Realiza a matrícula de um aluno em um curso.",
        Description = "**Acesso:** Requer usuário autenticado."
    )]
    [SwaggerResponse(StatusCodes.Status202Accepted, "Aluno matriculado com sucesso.", typeof(MensagemOutputDto))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Acesso negado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Aluno ou Curso não encontrados.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Aluno já está matriculado neste curso.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status422UnprocessableEntity, "Erro de validação.", typeof(ProblemDetails))]
    public async Task<IActionResult> Post([FromBody] EnrollmentInputDto input)
    {
        var command = new CreateEnrollmentCommand(input.StudentId, input.CourseId);
        await _mediator.Send(command);

        return Accepted(new MensagemOutputDto("Aluno matriculado com sucesso."));
    }
}
