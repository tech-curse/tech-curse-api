using MediatR;
using Microsoft.AspNetCore.Identity;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Application.Features.Students.Commands.DeleteStudent;

public class DeleteStudentCommandHandler : IRequestHandler<DeleteStudentCommand>
{
    private readonly IStudentRepository _studentRepository;
    private readonly ICacheService _cacheService;
    private readonly UserManager<IdentityUser> _userManager;

    private const string STUDENT_ITEM_PREFIX = "students:item:";
    private const string STUDENT_LIST_PREFIX = "students:list:";

    public DeleteStudentCommandHandler(
        IStudentRepository studentRepository,
        ICacheService cacheService,
        UserManager<IdentityUser> userManager)
    {
        _studentRepository = studentRepository;
        _cacheService = cacheService;
        _userManager = userManager;
    }

    public async Task Handle(DeleteStudentCommand request, CancellationToken cancellationToken)
    {
        var student = await _studentRepository.GetByIdAsync(request.Id);

        if (student == null || student.IsDeleted)
        {
            throw new NotFoundException("Estudante não encontrado.");
        }

        student.IsDeleted = true;
        student.DeletedAt = DateTime.UtcNow;

        await _studentRepository.UpdateAsync(student);

        if (student.IdentityUser != null)
        {
            await _userManager.SetLockoutEndDateAsync(student.IdentityUser, DateTimeOffset.MaxValue);
        }

        await _cacheService.RemoveAsync($"{STUDENT_ITEM_PREFIX}{request.Id}");
        await _cacheService.RemoveByPrefixAsync(STUDENT_LIST_PREFIX);
    }
}
