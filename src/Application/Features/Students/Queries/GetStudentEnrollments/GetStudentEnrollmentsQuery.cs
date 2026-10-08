using MediatR;
using TechCurse.Application.DTOs;

namespace TechCurse.Application.Features.Students.Queries.GetStudentEnrollments;

public record GetStudentEnrollmentsQuery(int Id) : IRequest<IEnumerable<CourseStudentOutputDto>>;
