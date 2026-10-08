using MediatR;
using TechCurse.Application.DTOs;

namespace TechCurse.Application.Features.Courses.Commands.CreateCourse;

public record CreateCourseCommand(string Titulo, string Descricao, string Categoria, int CargaHoraria) : IRequest<CourseOutputDto>;
