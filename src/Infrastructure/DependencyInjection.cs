using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TechCurse.Application.Interfaces;
using TechCurse.Infrastructure.ExternalServices;
using TechCurse.Infrastructure.Identity;
using TechCurse.Infrastructure.Repositories;

namespace TechCurse.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
        {
            services.AddScoped<ICourseRepository, CourseRepository>();
            services.AddScoped<IStudentRepository, StudentRepository>();
            services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
            services.AddScoped<IPaymentRepository, PaymentRepository>();

            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<ICacheService, RedisCacheService>();

            if (environment.IsProduction())
            {
                throw new InvalidOperationException(
                    "Nenhum gateway de pagamento real está configurado para o ambiente de Produção. " +
                    "A única implementação disponível de IPaymentGatewayAdapter é o " +
                    "SimulatedPaymentGatewayAdapter, que apenas simula transações e não realiza " +
                    "cobranças reais. Implemente e registre um adaptador de gateway real em " +
                    "TechCurse.Infrastructure.ExternalServices antes de subir a aplicação em Produção.");
            }

            services.AddScoped<IPaymentGatewayAdapter, SimulatedPaymentGatewayAdapter>();

            return services;
        }
    }
}
