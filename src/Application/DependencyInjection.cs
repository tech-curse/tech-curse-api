using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TechCurse.Application.Common.Behaviors;
using TechCurse.Application.Factory;
using TechCurse.Application.Interfaces;
using TechCurse.Application.Strategies;

namespace TechCurse.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IPaymentStrategy, CreditCardPaymentStrategy>();
            services.AddScoped<PaymentStrategyFactory>();

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
                cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            });
            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

            return services;
        }
    }
}
