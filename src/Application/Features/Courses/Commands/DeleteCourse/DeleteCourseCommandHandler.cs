using MediatR;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Application.Features.Courses.Commands.DeleteCourse;

public class DeleteCourseCommandHandler : IRequestHandler<DeleteCourseCommand, Unit>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ICacheService _cacheService;

    private const string COURSE_ITEM_PREFIX = "courses:item:";
    private const string COURSE_LIST_PREFIX = "courses:list:";

    public DeleteCourseCommandHandler(ICourseRepository courseRepository, ICacheService cacheService)
    {
        _courseRepository = courseRepository;
        _cacheService = cacheService;
    }

    public async Task<Unit> Handle(DeleteCourseCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdAsync(request.Id);

        if (course == null)
        {
            throw new NotFoundException("Curso não encontrado.");
        }

        var hasEnrollments = await _courseRepository.HasEnrollmentsAsync(request.Id);
        if (hasEnrollments)
        {
            throw new ConflictException("O curso possui matrículas ativas.");
        }

        await _courseRepository.DeleteAsync(course);

        await _cacheService.RemoveAsync($"{COURSE_ITEM_PREFIX}{request.Id}");
        await _cacheService.RemoveByPrefixAsync(COURSE_LIST_PREFIX);

        return Unit.Value;
    }
}
