using ErrorOr;

namespace VPD.Domain.Common.Errors;

public static partial class Errors
{
    public static class User
    {
        public static Error DuplicateEmailError() => Error.Conflict(
            code: "User.DuplicateEmail",
            description: "A user with the given username already exists."
        );
        
        public static Error NotFoundUserWithIdError() => Error.NotFound(
            code: "User.NotFoundUserWithId",
            description: "A user with the given id does not exist."
        );
    }
}