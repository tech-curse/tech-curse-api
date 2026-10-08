using MediatR;

namespace TechCurse.Application.Features.Enrollments.Commands.CreateEnrollment;

public record CreateEnrollmentCommand(int StudentId, int CourseId) : IRequest;
