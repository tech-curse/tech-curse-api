using MediatR;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;

namespace TechCurse.Application.Features.Courses.Queries.GetCourses;

public class GetCoursesQueryHandler : IRequestHandler<GetCoursesQuery, PagedResultDto<CourseOutputDto>>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ICacheService _cacheService;

    private const string COURSE_LIST_PREFIX = "courses:list:";

    public GetCoursesQueryHandler(ICourseRepository courseRepository, ICacheService cacheService)
    {
        _courseRepository = courseRepository;
        _cacheService = cacheService;
    }

    public async Task<PagedResultDto<CourseOutputDto>> Handle(GetCoursesQuery request, CancellationToken cancellationToken)
    {
        var searchParams = request.SearchParams;
        var cacheKey = $"{COURSE_LIST_PREFIX}page:{searchParams.PageNumber}:size:{searchParams.PageSize}:sort:{searchParams.SortBy}_{searchParams.SortDirection}";

        if (!string.IsNullOrWhiteSpace(searchParams.Categoria))
        {
            cacheKey = $"{COURSE_LIST_PREFIX}cat:{searchParams.Categoria}:page:{searchParams.PageNumber}:size:{searchParams.PageSize}:sort:{searchParams.SortBy}_{searchParams.SortDirection}";
        }

        var cachedResult = await _cacheService.GetAsync<PagedResultDto<CourseOutputDto>>(cacheKey);
        if (cachedResult != null)
        {
            return cachedResult;
        }

        var (courses, totalCount) = await _courseRepository.GetPagedAsync(searchParams);

        var dtos = courses.Select(c => new CourseOutputDto(c.CourseId, c.Titulo, c.Descricao, c.Categoria, c.CargaHoraria, c.DataCriacao)).ToList();

        var result = new PagedResultDto<CourseOutputDto>(
            dtos,
            totalCount,
            searchParams.PageNumber,
            searchParams.PageSize
        );

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(15));

        return result;
    }
}
