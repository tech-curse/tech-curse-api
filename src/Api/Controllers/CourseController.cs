using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using TechCurse.Application.DTOs;
using TechCurse.Application.Features.Courses.Commands.CreateCourse;
using TechCurse.Application.Features.Courses.Commands.DeleteCourse;
using TechCurse.Application.Features.Courses.Commands.UpdateCourse;
using TechCurse.Application.Features.Courses.Queries.GetCourseById;
using TechCurse.Application.Features.Courses.Queries.GetCourses;
using TechCurse.Application.Interfaces;

namespace TechCurse.Api.Controllers;

[ApiController]
[Route("tech-curse/[controller]")]
[Consumes("application/json")]
[Produces("application/json")]
[Tags("Courses")]
public class CourseController : ControllerBase
{
    private readonly IMediator _mediator;

    public CourseController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Instructor")]
    [SwaggerOperation(
        Summary = "Cria um novo curso.",
        Description = "**Acesso:** Requer role de Admin ou Instructor."
    )]
    [SwaggerResponse(StatusCodes.Status201Created, "Curso criado com sucesso.", typeof(CourseOutputDto))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Acesso negado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status422UnprocessableEntity, "Erro de validação.", typeof(ProblemDetails))]
    public async Task<IActionResult> Post([FromBody] CreateCourseCommand command)
    {
        var result = await _mediator.Send(command);

        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpGet]
    [Authorize]
    [SwaggerOperation(
        Summary = "Retorna uma lista paginada de cursos.",
        Description = "**Acesso:** Requer autenticação."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Lista de cursos retornada com sucesso.", typeof(PagedResultDto<CourseOutputDto>))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado.", typeof(ProblemDetails))]
    public async Task<IActionResult> GetAll([FromQuery] CoursePaginationParamsDto searchParams)
    {
        var result = await _mediator.Send(new GetCoursesQuery(searchParams));
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize]
    [SwaggerOperation(
        Summary = "Retorna os detalhes de um curso específico.",
        Description = "**Acesso:** Requer autenticação."
    )]
    [SwaggerResponse(StatusCodes.Status200OK, "Detalhes do curso retornados com sucesso.", typeof(CourseOutputDto))]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Curso não encontrado.", typeof(ProblemDetails))]
    public async Task<IActionResult> Get(int id)
    {
        var result = await _mediator.Send(new GetCourseByIdQuery(id));
        return Ok(result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(
        Summary = "Atualiza os dados de um curso existente.",
        Description = "**Acesso:** Requer role de Admin."
    )]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Curso atualizado com sucesso.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Acesso negado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Curso não encontrado.", typeof(ProblemDetails))]
    public async Task<IActionResult> Put(int id, [FromBody] CoursePostDto input)
    {
        var command = new UpdateCourseCommand(id, input.Titulo, input.Descricao, input.Categoria, input.CargaHoraria);
        await _mediator.Send(command);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(
        Summary = "Exclui um curso existente.",
        Description = "**Acesso:** Requer role de Admin."
    )]
    [SwaggerResponse(StatusCodes.Status204NoContent, "Curso excluído com sucesso.")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "Usuário não autenticado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "Acesso negado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Curso não encontrado.", typeof(ProblemDetails))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Conflito. O curso possui matrículas ativas.", typeof(ProblemDetails))]
    public async Task<IActionResult> Delete(int id)
    {
        var command = new DeleteCourseCommand(id);
        await _mediator.Send(command);
        return NoContent();
    }
}
