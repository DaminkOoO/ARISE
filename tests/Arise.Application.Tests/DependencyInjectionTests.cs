using Arise.Application.Common.Behaviors;
using Arise.Application.Common.Diagnostics;
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
    public async Task Applique_a_une_requete_envoyee_les_validators_decouverts_dans_l_assembly_Application()
    {
        // Ferme d'un seul tenant la couture « découverte des validators + pipeline MediatR ».
        // Le mode de panne visé est silencieux : si la découverte ne trouve rien, le
        // ValidationBehavior s'exécute avec zéro validator et laisse tout passer sans rougir.
        // La sonde vit dans l'assembly Application parce que c'est celle qui est balayée.
        var mediator = Provider().GetRequiredService<IMediator>();

        var acte = async () => await mediator.Send(new PipelineValidationProbe(""));

        (await acte.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should()
            .Be(PipelineValidationProbeValidator.MessageValeurObligatoire);
    }

    [Fact]
    public async Task Laisse_une_requete_valide_atteindre_son_handler()
    {
        var mediator = Provider().GetRequiredService<IMediator>();

        var resultat = await mediator.Send(new PipelineValidationProbe("valeur"));

        resultat.Should().Be(PipelineValidationProbeHandler.Reponse);
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
