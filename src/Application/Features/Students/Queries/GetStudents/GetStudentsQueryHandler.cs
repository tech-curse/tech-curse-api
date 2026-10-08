using MediatR;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;

namespace TechCurse.Application.Features.Students.Queries.GetStudents;

public class GetStudentsQueryHandler : IRequestHandler<GetStudentsQuery, PagedResultDto<StudentOutputDto>>
{
    private readonly IStudentRepository _studentRepository;
    private readonly ICacheService _cacheService;

    private const string STUDENT_LIST_PREFIX = "students:list:";

    public GetStudentsQueryHandler(IStudentRepository studentRepository, ICacheService cacheService)
    {
        _studentRepository = studentRepository;
        _cacheService = cacheService;
    }

    public async Task<PagedResultDto<StudentOutputDto>> Handle(GetStudentsQuery request, CancellationToken cancellationToken)
    {
        var searchParams = request.SearchParams;
        var cacheKey = $"{STUDENT_LIST_PREFIX}page:{searchParams.PageNumber}:size:{searchParams.PageSize}:sort:{searchParams.SortBy}_{searchParams.SortDirection}";

        var cachedResult = await _cacheService.GetAsync<PagedResultDto<StudentOutputDto>>(cacheKey);
        if (cachedResult != null)
        {
            return cachedResult;
        }

        var (students, totalCount) = await _studentRepository.GetPagedAsync(searchParams);

        var dtos = students.Select(c => new StudentOutputDto(c.StudentId, c.Nome, c.Email, c.DataCadastro));

        var result = new PagedResultDto<StudentOutputDto>(
            dtos,
            totalCount,
            searchParams.PageNumber,
            searchParams.PageSize
        );

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(15));

        return result;
    }
}
