using MediatR;

namespace TechCurse.Application.Features.Students.Commands.UpdateStudent;

public record UpdateStudentCommand(int Id, string Nome) : IRequest;
