using Arise.Application.Common.Behaviors;
using Arise.Application.Common.Diagnostics;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
// Alias : le namespace DataAnnotations expose lui aussi une ValidationException, qui
// entrerait en collision avec celle de FluentValidation utilisée plus bas.
using DisplayAttribute = System.ComponentModel.DataAnnotations.DisplayAttribute;

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

    // Règle non négociable n°7 : tout texte visible par l'utilisateur est en français,
    // messages de validation compris — ils remontent jusqu'à l'écran. Le gabarit traduit
    // ne suffit pas : le {PropertyName} interpolé dedans en fait partie.

    public sealed record RequeteEtiquetee(
        [property: Display(Name = "nom du Chasseur")] string NomDuChasseur)
        : IRequest<string>;

    public sealed record RequeteSansEtiquette(string NomDuChasseur) : IRequest<string>;

    [Fact]
    public void Formule_en_francais_le_message_par_defaut_nom_de_propriete_compris()
    {
        Provider();

        var resultat = new ValidatorEtiquete().Validate(new RequeteEtiquetee(""));

        resultat.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("'nom du Chasseur' ne doit pas être vide.");
    }

    [Fact]
    public void N_habille_pas_en_prose_le_nom_d_une_propriete_sans_etiquette_francaise()
    {
        // Le découpage PascalCase par défaut fabrique du franglais présentable
        // ('Nom Du Chasseur', 'Email Address') qui passe la revue sans se faire remarquer.
        // Laisser l'identifiant brut rend l'étiquette manquante visible.
        Provider();

        var resultat = new ValidatorSansEtiquette().Validate(new RequeteSansEtiquette(""));

        resultat.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("'NomDuChasseur' ne doit pas être vide.");
    }

    private sealed class ValidatorEtiquete : AbstractValidator<RequeteEtiquetee>
    {
        public ValidatorEtiquete() => RuleFor(r => r.NomDuChasseur).NotEmpty();
    }

    private sealed class ValidatorSansEtiquette : AbstractValidator<RequeteSansEtiquette>
    {
        public ValidatorSansEtiquette() => RuleFor(r => r.NomDuChasseur).NotEmpty();
    }
}
