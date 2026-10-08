using MediatR;
using TechCurse.Application.DTOs;

namespace TechCurse.Application.Features.Courses.Queries.GetCourseById;

public record GetCourseByIdQuery(int Id) : IRequest<CourseOutputDto>;
