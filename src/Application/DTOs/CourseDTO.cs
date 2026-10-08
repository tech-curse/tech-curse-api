using TechCurse.Domain.Entities;

namespace TechCurse.Application.DTOs;

public record CoursePostDto(string Titulo, string Descricao, string Categoria, int CargaHoraria);
public record CoursePutDto(int Id, string Titulo, string Descricao, string Categoria, int CargaHoraria);
public record CourseOutputDto(int Id, string Titulo, string Descricao, string Categoria, int CargaHoraria, DateTime DataCriacao);
