using Arise.Application.Common.Behaviors;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Arise.Application.Tests;

public class DependencyInjectionTests
{
    public sealed record RequeteFactice(string Nom) : IRequest<string>;

    private static ServiceProvider Provider() =>
        new ServiceCollection().AddApplication().BuildServiceProvider();

    [Fact]
    public void Expose_un_IMediator_resolvable()
    {
        Provider().GetService<IMediator>().Should().NotBeNull();
    }

    [Fact]
    public void Branche_le_ValidationBehavior_sur_le_pipeline()
    {
        var behaviors = Provider()
            .GetServices<IPipelineBehavior<RequeteFactice, string>>();

        behaviors.Should().ContainItemsAssignableTo<ValidationBehavior<RequeteFactice, string>>();
    }

    [Fact]
    public void Enregistre_les_validators_declares_dans_l_assembly_Application()
    {
        // Le marqueur d'assembly détermine où AddApplication va chercher handlers et
        // validators : s'il pointe ailleurs, le pipeline se retrouve silencieusement vide.
        typeof(ValidationBehavior<,>).Assembly
            .Should().BeSameAs(Arise.Application.DependencyInjection.AssemblyApplication);
    }

    [Fact]
    public void Formule_les_messages_de_validation_par_defaut_en_francais()
    {
        // Règle non négociable n°7 : tout texte visible par l'utilisateur est en français,
        // messages de validation compris — ils remontent jusqu'à l'écran.
        Provider();

        var resultat = new ValidatorSansMessageExplicite().Validate(new RequeteFactice(""));

        resultat.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain("ne doit pas être vide");
    }

    private sealed class ValidatorSansMessageExplicite : AbstractValidator<RequeteFactice>
    {
        public ValidatorSansMessageExplicite() => RuleFor(r => r.Nom).NotEmpty();
    }
}
