using MediatR;
using Microsoft.AspNetCore.Identity;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Application.Features.Students.Commands.CreateStudent;

public class CreateStudentCommandHandler : IRequestHandler<CreateStudentCommand, StudentOutputDto>
{
    private readonly IStudentRepository _studentRepository;
    private readonly UserManager<IdentityUser> _userManager;

    public CreateStudentCommandHandler(IStudentRepository studentRepository, UserManager<IdentityUser> userManager)
    {
        _studentRepository = studentRepository;
        _userManager = userManager;
    }

    public async Task<StudentOutputDto> Handle(CreateStudentCommand request, CancellationToken cancellationToken)
    {
        bool emailExists = await _studentRepository.EmailExistsAsync(request.Email);
        if (emailExists)
        {
            throw new ConflictException("O e-mail informado já está em uso por outro estudante.");
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            throw new ConflictException("Usuário não encontrado.");
        }

        var student = new Student
        {
            Nome = request.Nome,
            Email = request.Email,
            DataCadastro = DateTime.UtcNow,
            IdentityUserId = user.Id,
            IdentityUser = user,
            IsDeleted = false,
            Enrollments = new List<Enrollment>()
        };

        await _studentRepository.AddAsync(student);

        return new StudentOutputDto(student.StudentId, student.Nome, student.Email, student.DataCadastro);
    }
}
