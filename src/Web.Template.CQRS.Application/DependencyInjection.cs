using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Web.Template.CQRS.Application.Common.Behaviors;

namespace Web.Template.CQRS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.PipelineBehaviors = [typeof(ValidationBehavior<,>)];
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
