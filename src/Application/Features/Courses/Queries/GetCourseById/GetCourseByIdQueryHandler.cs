using MediatR;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Application.Features.Courses.Queries.GetCourseById;

public class GetCourseByIdQueryHandler : IRequestHandler<GetCourseByIdQuery, CourseOutputDto>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ICacheService _cacheService;

    private const string COURSE_ITEM_PREFIX = "courses:item:";

    public GetCourseByIdQueryHandler(ICourseRepository courseRepository, ICacheService cacheService)
    {
        _courseRepository = courseRepository;
        _cacheService = cacheService;
    }

    public async Task<CourseOutputDto> Handle(GetCourseByIdQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"{COURSE_ITEM_PREFIX}{request.Id}";

        var cachedCourse = await _cacheService.GetAsync<CourseOutputDto>(cacheKey);
        if (cachedCourse != null)
        {
            return cachedCourse;
        }

        var course = await _courseRepository.GetByIdAsync(request.Id);

        if (course == null)
        {
            throw new NotFoundException("Curso não encontrado.");
        }

        var result = new CourseOutputDto(course.CourseId, course.Titulo, course.Descricao, course.Categoria, course.CargaHoraria, course.DataCriacao);

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(15));

        return result;
    }
}
