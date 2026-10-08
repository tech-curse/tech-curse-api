using MediatR;
using TechCurse.Application.DTOs;

namespace TechCurse.Application.Features.Courses.Queries.GetCourses;

public record GetCoursesQuery(CoursePaginationParamsDto SearchParams) : IRequest<PagedResultDto<CourseOutputDto>>;
