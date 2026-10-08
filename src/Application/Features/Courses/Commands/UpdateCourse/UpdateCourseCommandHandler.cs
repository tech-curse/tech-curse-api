using MediatR;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Application.Features.Courses.Commands.UpdateCourse;

public class UpdateCourseCommandHandler : IRequestHandler<UpdateCourseCommand, Unit>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ICacheService _cacheService;

    private const string COURSE_ITEM_PREFIX = "courses:item:";
    private const string COURSE_LIST_PREFIX = "courses:list:";

    public UpdateCourseCommandHandler(ICourseRepository courseRepository, ICacheService cacheService)
    {
        _courseRepository = courseRepository;
        _cacheService = cacheService;
    }

    public async Task<Unit> Handle(UpdateCourseCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdAsync(request.Id);

        if (course == null)
        {
            throw new NotFoundException("Curso não encontrado.");
        }

        course.Titulo = request.Titulo;
        course.Descricao = request.Descricao;
        course.Categoria = request.Categoria;
        course.CargaHoraria = request.CargaHoraria;

        await _courseRepository.UpdateAsync(course);

        var updatedDto = new CourseOutputDto(course.CourseId, course.Titulo, course.Descricao, course.Categoria, course.CargaHoraria, course.DataCriacao);
        await _cacheService.SetAsync($"{COURSE_ITEM_PREFIX}{request.Id}", updatedDto, TimeSpan.FromMinutes(15));

        await _cacheService.RemoveByPrefixAsync(COURSE_LIST_PREFIX);

        return Unit.Value;
    }
}
