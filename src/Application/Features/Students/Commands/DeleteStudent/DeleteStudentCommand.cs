using MediatR;

namespace TechCurse.Application.Features.Students.Commands.DeleteStudent;

public record DeleteStudentCommand(int Id) : IRequest;
