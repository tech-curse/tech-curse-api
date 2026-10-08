using MediatR;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Enums;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Application.Features.Students.Commands.UpdateStudent;

public class UpdateStudentCommandHandler : IRequestHandler<UpdateStudentCommand>
{
    private readonly IStudentRepository _studentRepository;
    private readonly ICacheService _cacheService;
    private readonly ICurrentUserService _currentUserService;

    private const string STUDENT_ITEM_PREFIX = "students:item:";
    private const string STUDENT_LIST_PREFIX = "students:list:";

    public UpdateStudentCommandHandler(
        IStudentRepository studentRepository,
        ICacheService cacheService,
        ICurrentUserService currentUserService)
    {
        _studentRepository = studentRepository;
        _cacheService = cacheService;
        _currentUserService = currentUserService;
    }

    private void ValidateRoleAccess(string targetIdentityUserId)
    {
        var currentUserId = _currentUserService.GetUserId();
        var isAdmin = _currentUserService.IsInRole(UserRole.Admin);

        if (currentUserId != targetIdentityUserId && !isAdmin)
        {
            throw new NotAllowedException("Você não possui permissão suficiente para atualizar este registro.");
        }
    }

    public async Task Handle(UpdateStudentCommand request, CancellationToken cancellationToken)
    {
        var student = await _studentRepository.GetByIdAsync(request.Id);

        if (student == null || student.IsDeleted)
        {
            throw new NotFoundException("Estudante não encontrado.");
        }

        ValidateRoleAccess(student.IdentityUserId);

        student.Nome = request.Nome;

        await _studentRepository.UpdateAsync(student);

        var updatedDto = new StudentOutputDto(student.StudentId, student.Nome, student.Email, student.DataCadastro);
        await _cacheService.SetAsync($"{STUDENT_ITEM_PREFIX}{request.Id}", updatedDto, TimeSpan.FromMinutes(15));

        await _cacheService.RemoveByPrefixAsync(STUDENT_LIST_PREFIX);
    }
}
