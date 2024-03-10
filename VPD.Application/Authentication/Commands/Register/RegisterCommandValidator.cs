using FluentValidation;

namespace VPD.Application.Authentication.Commands.Register;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}