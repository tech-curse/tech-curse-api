using MediatR;
using TechCurse.Application.DTOs;

namespace TechCurse.Application.Features.Students.Queries.GetStudents;

public record GetStudentsQuery(PaginationParamsDto SearchParams) : IRequest<PagedResultDto<StudentOutputDto>>;
