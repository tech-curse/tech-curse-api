using MediatR;

namespace TechCurse.Application.Features.Courses.Commands.DeleteCourse;

public record DeleteCourseCommand(int Id) : IRequest<Unit>;
