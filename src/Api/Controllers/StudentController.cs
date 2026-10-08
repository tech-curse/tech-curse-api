using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TechCurse.Application.DTOs;
using TechCurse.Application.Features.Students.Commands.CreateStudent;
using TechCurse.Application.Features.Students.Commands.DeleteStudent;
using TechCurse.Application.Features.Students.Commands.UpdateStudent;
using TechCurse.Application.Features.Students.Queries.GetSelfStudent;
using TechCurse.Application.Features.Students.Queries.GetStudentById;
using TechCurse.Application.Features.Students.Queries.GetStudentEnrollments;
using TechCurse.Application.Features.Students.Queries.GetStudents;
using TechCurse.Application.Interfaces;

namespace TechCurse.Api.Controllers;

[ApiController]
[Route("tech-curse/[controller]")]
[Consumes("application/json")]
[Produces("application/json")]
[Tags("Students")]
public class StudentController : ControllerBase
{
    private readonly IMediator _mediator;

    public StudentController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(
                Summary = "Cria um novo estudante no sistema.",
                Description = "**Acesso:** Requer role de Admin."
            )]
    [SwaggerResponse(StatusCodes.Status201Created, "Estudante criado com sucesso.", typeof(StudentOutputDto))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Acesso negado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "E-mail ou documento já em uso.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status422UnprocessableEntity, "Erro de validação.", typeof(ProblemDetails))]
    public async Task<IActionResult> Post([FromBody] StudentPostDto input)
    {
        var command = new CreateStudentCommand(input.Nome, input.Email);
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(
        Summary = "Lista os estudantes cadastrados (paginado).",
        Description = "**Acesso:** Requer role de Admin."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Retorna a lista paginada.", typeof(PagedResultDto<StudentOutputDto>))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Acesso negado.", typeof(ProblemDetails))]
    public async Task<IActionResult> GetAll([FromQuery] PaginationParamsDto searchParams)
    {
        var query = new GetStudentsQuery(searchParams);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize]
    [SwaggerOperation(
        Summary = "Busca os detalhes de um estudante específico pelo ID.",
        Description = "**Acesso:** Requer usuário autenticado."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Estudante encontrado.", typeof(StudentOutputDto))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Estudante não encontrado.", typeof(ProblemDetails))]
    public async Task<IActionResult> Get(int id)
    {
        var query = new GetStudentByIdQuery(id);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("{id}/enrollments")]
    [Authorize]
    [SwaggerOperation(
        Summary = "Lista os cursos nos quais o estudante está matriculado.",
        Description = "**Acesso:** Requer usuário autenticado."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Lista de matrículas encontrada.", typeof(IEnumerable<CourseStudentOutputDto>))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Estudante não encontrado.", typeof(ProblemDetails))]
    public async Task<IActionResult> GetEnrollments(int id)
    {
        var query = new GetStudentEnrollmentsQuery(id);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize(Roles = "Student")]
    [SwaggerOperation(
        Summary = "Busca os dados do próprio estudante logado.",
        Description = "**Acesso:** Requer role de Student (extraído via Token JWT)."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Dados do estudante.", typeof(StudentOutputDto))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Acesso negado.", typeof(ProblemDetails))]
    public async Task<IActionResult> GetSelf()
    {
        var query = new GetSelfStudentQuery();
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpPut("{id}")]
    [Authorize]
    [SwaggerOperation(
                Summary = "Atualiza os dados de um estudante.",
                Description = "**Acesso:** Requer usuário autenticado."
            )]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Estudante atualizado com sucesso.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Estudante não encontrado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status422UnprocessableEntity, "Erro de validação.", typeof(ProblemDetails))]
    public async Task<IActionResult> Put(int id, [FromBody] StudentPutDto input)
    {
        var command = new UpdateStudentCommand(id, input.Nome);
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(
        Summary = "Deleta (soft delete) um estudante pelo ID.",
        Description = "**Acesso:** Requer role de Admin."
    )]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Estudante excluído com sucesso.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Acesso negado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Estudante não encontrado.", typeof(ProblemDetails))]
    public async Task<IActionResult> Delete(int id)
    {
        var command = new DeleteStudentCommand(id);
        await _mediator.Send(command);
        return NoContent();
    }
}
