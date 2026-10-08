using MediatR;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Application.Features.Students.Queries.GetSelfStudent;

public class GetSelfStudentQueryHandler : IRequestHandler<GetSelfStudentQuery, StudentOutputDto>
{
    private readonly IStudentRepository _studentRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetSelfStudentQueryHandler(
        IStudentRepository studentRepository,
        ICurrentUserService currentUserService)
    {
        _studentRepository = studentRepository;
        _currentUserService = currentUserService;
    }

    public async Task<StudentOutputDto> Handle(GetSelfStudentQuery request, CancellationToken cancellationToken)
    {
        var currentUserEmail = _currentUserService.GetUserEmail();
        if (currentUserEmail == null)
        {
            throw new NotAllowedException("Email do usuário não encontrado!");
        }

        var student = await _studentRepository.GetByEmailAsync(currentUserEmail);

        if (student == null || student.IsDeleted)
        {
            throw new NotFoundException("Perfil de estudante não encontrado ou inativo.");
        }

        return new StudentOutputDto(student.StudentId, student.Nome, student.Email, student.DataCadastro);
    }
}
