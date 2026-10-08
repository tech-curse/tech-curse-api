using MediatR;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Entities;
using TechCurse.Domain.Enums;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Application.Features.Enrollments.Commands.CreateEnrollment;

public class CreateEnrollmentCommandHandler : IRequestHandler<CreateEnrollmentCommand>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly ICourseRepository _courseRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IEnrollmentRepository _enrollmentRepository;

    public CreateEnrollmentCommandHandler(
        ICurrentUserService currentUserService,
        ICourseRepository courseRepository,
        IStudentRepository studentRepository,
        IEnrollmentRepository enrollmentRepository)
    {
        _currentUserService = currentUserService;
        _courseRepository = courseRepository;
        _studentRepository = studentRepository;
        _enrollmentRepository = enrollmentRepository;
    }

    public async Task Handle(CreateEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var isAdmin = _currentUserService.IsInRole(UserRole.Admin);
        var isStudent = _currentUserService.IsInRole(UserRole.Student);

        if (!isStudent && !isAdmin)
        {
            throw new NotAllowedException("Apenas estudantes e administradores podem criar matrículas!");
        }

        var userEmail = _currentUserService.GetUserEmail();
        if (userEmail == null)
        {
            throw new NotAllowedException("Email do usuário não encontrado!");
        }

        var student = isAdmin
            ? await _studentRepository.GetByIdAsync(request.StudentId)
            : await _studentRepository.GetByEmailAsync(userEmail);

        if (student == null)
        {
            throw new NotFoundException("Estudante não encontrado!");
        }

        if (!await _studentRepository.StudentIsActiveAsync(student))
        {
            throw new NotAllowedException("Estudante não está ativo!");
        }

        var course = await _courseRepository.GetByIdAsync(request.CourseId);
        if (course == null)
        {
            throw new NotFoundException("Curso não encontrado!");
        }

        var existingEnrollment = await _enrollmentRepository.GetByStudentCourseAsync(student.StudentId, course.CourseId);
        if (existingEnrollment != null)
        {
            throw new ConflictException("Estudante já está matriculado neste curso!");
        }

        Enrollment enrollment = new Enrollment
        {
            StudentId = student.StudentId,
            CourseId = course.CourseId,
            DataMatricula = DateTime.UtcNow
        };

        await _enrollmentRepository.AddAsync(enrollment);
    }
}
