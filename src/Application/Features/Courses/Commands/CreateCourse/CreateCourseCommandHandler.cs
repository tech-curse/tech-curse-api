using MediatR;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;

namespace TechCurse.Application.Features.Courses.Commands.CreateCourse;

public class CreateCourseCommandHandler : IRequestHandler<CreateCourseCommand, CourseOutputDto>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ICacheService _cacheService;

    private const string COURSE_ITEM_PREFIX = "courses:item:";
    private const string COURSE_LIST_PREFIX = "courses:list:";

    public CreateCourseCommandHandler(ICourseRepository courseRepository, ICacheService cacheService)
    {
        _courseRepository = courseRepository;
        _cacheService = cacheService;
    }

    public async Task<CourseOutputDto> Handle(CreateCourseCommand request, CancellationToken cancellationToken)
    {
        var course = new Course
        {
            Titulo = request.Titulo,
            Descricao = request.Descricao,
            Categoria = request.Categoria,
            CargaHoraria = request.CargaHoraria,
            DataCriacao = DateTime.UtcNow
        };

        await _courseRepository.AddAsync(course);

        var result = new CourseOutputDto(course.CourseId, course.Titulo, course.Descricao, course.Categoria, course.CargaHoraria, course.DataCriacao);

        var cacheKey = $"{COURSE_ITEM_PREFIX}{course.CourseId}";
        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(15));

        await _cacheService.RemoveByPrefixAsync(COURSE_LIST_PREFIX);

        return result;
    }
}
