using FluentValidation;
using Mediator;
using Web.Template.CQRS.Application.Common.Behaviors;

namespace Web.Template.CQRS.Application.UnitTests.Common;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_WithInvalidMessage_ThrowsValidationExceptionWithoutCallingNext()
    {
        var behavior = new ValidationBehavior<SampleCommand, string>([new SampleCommandValidator()]);
        var nextCalled = false;

        var exception = await Assert.ThrowsAsync<ValidationException>(async () =>
            await behavior.Handle(
                new SampleCommand(string.Empty),
                (_, _) =>
                {
                    nextCalled = true;
                    return ValueTask.FromResult("handled");
                },
                TestContext.Current.CancellationToken));

        Assert.False(nextCalled);
        var failure = Assert.Single(exception.Errors);
        Assert.Equal(nameof(SampleCommand.Name), failure.PropertyName);
    }

    [Fact]
    public async Task Handle_WithValidMessage_CallsNext()
    {
        var behavior = new ValidationBehavior<SampleCommand, string>([new SampleCommandValidator()]);

        var result = await behavior.Handle(
            new SampleCommand("valid"),
            (_, _) => ValueTask.FromResult("handled"),
            TestContext.Current.CancellationToken);

        Assert.Equal("handled", result);
    }

    public sealed record SampleCommand(string Name) : ICommand<string>;

    public sealed class SampleCommandValidator : AbstractValidator<SampleCommand>
    {
        public SampleCommandValidator()
        {
            RuleFor(command => command.Name).NotEmpty();
        }
    }
}
