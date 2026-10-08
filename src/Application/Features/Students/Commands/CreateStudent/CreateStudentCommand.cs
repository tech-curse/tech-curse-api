using MediatR;
using TechCurse.Application.DTOs;

namespace TechCurse.Application.Features.Students.Commands.CreateStudent;

public record CreateStudentCommand(string Nome, string Email) : IRequest<StudentOutputDto>;
