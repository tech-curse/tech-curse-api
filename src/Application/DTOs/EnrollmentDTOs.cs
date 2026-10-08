using System.ComponentModel.DataAnnotations;

namespace TechCurse.Application.DTOs;

public record EnrollmentInputDto([Required] int CourseId, int StudentId);
