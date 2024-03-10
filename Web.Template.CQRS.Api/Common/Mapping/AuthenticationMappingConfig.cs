using Mapster;
using Web.Template.CQRS.Application.Authentication.Common;
using Web.Template.CQRS.Contracts.Authentication;

namespace Web.Template.CQRS.Api.Common.Mapping;

public class AuthenticationMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<AuthenticationResult, AuthenticationResponse>()
            .BeforeMapping((src, dest) => Console.WriteLine(src))
            .Map(dest => dest.Token, src => src.Token)
            .Map(dest => dest.Id, src => src.User.Id.Value)
            .Map(dest => dest, src => src.User);
    }
}