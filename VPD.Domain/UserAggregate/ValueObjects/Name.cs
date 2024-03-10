using VPD.Domain.Common.Models;

namespace VPD.Domain.UserAggregate.ValueObjects;

public sealed class Name(string firstName, string lastName) : ValueObject
{
    public string FirstName { get; protected set; } = firstName;
    public string LastName { get; protected set; } = lastName;

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return FirstName;
        yield return LastName;
    }
}