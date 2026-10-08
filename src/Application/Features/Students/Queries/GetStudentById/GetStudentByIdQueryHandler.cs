using MediatR;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Enums;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Application.Features.Students.Queries.GetStudentById;

public class GetStudentByIdQueryHandler : IRequestHandler<GetStudentByIdQuery, StudentOutputDto>
{
    private readonly IStudentRepository _studentRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetStudentByIdQueryHandler(
        IStudentRepository studentRepository,
        ICurrentUserService currentUserService)
    {
        _studentRepository = studentRepository;
        _currentUserService = currentUserService;
    }

    private void ValidateRoleAccess(string targetIdentityUserId)
    {
        var currentUserId = _currentUserService.GetUserId();
        var isAdmin = _currentUserService.IsInRole(UserRole.Admin);

        if (currentUserId != targetIdentityUserId && !isAdmin)
        {
            throw new NotAllowedException("Você não possui permissão suficiente para acessar este registro.");
        }
    }

    public async Task<StudentOutputDto> Handle(GetStudentByIdQuery request, CancellationToken cancellationToken)
    {

        var student = await _studentRepository.GetByIdAsync(request.Id);

        if (student == null || student.IsDeleted)
        {
            throw new NotFoundException("Estudante não encontrado.");
        }

        ValidateRoleAccess(student.IdentityUserId);

        return new StudentOutputDto(student.StudentId, student.Nome, student.Email, student.DataCadastro);
    }
}
