using MediatR;
using TechCurse.Application.DTOs;

namespace TechCurse.Application.Features.Students.Queries.GetStudentById;

public record GetStudentByIdQuery(int Id) : IRequest<StudentOutputDto>;
