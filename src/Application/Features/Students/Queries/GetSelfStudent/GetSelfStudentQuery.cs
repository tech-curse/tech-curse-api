using MediatR;
using TechCurse.Application.DTOs;

namespace TechCurse.Application.Features.Students.Queries.GetSelfStudent;

public record GetSelfStudentQuery() : IRequest<StudentOutputDto>;
