using System.Linq.Expressions;
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
    public void N_enregistre_le_ValidationBehavior_qu_une_fois_si_appele_deux_fois()
    {
        // Deux appels enregistraient deux behaviors : chaque requête valide traversait la
        // validation en double. Sans effet visible tant que les validators sont purs, mais
        // le premier validator asynchrone interrogeant la base frapperait la base deux fois.
        var services = new ServiceCollection().AddApplication().AddApplication();

        using var provider = services.BuildServiceProvider();

        provider.GetServices<IPipelineBehavior<RequeteFactice, string>>()
            .Should().ContainSingle();
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

    private sealed class Requete
    {
        public string NomDuChasseur { get; init; } = "";
    }

    [Fact]
    public void Branche_le_resolveur_de_noms_francais_sur_FluentValidation()
    {
        // Ce que le résolveur répond est éprouvé à part, comme fonction pure
        // (ResolveurNomAffichableTests) : ValidatorOptions.Global est un statique global au
        // processus, et xUnit parallélise les classes de tests d'une même assembly. Ce test
        // ne couvre donc que le branchement, et sur une valeur qu'il produit lui-même.
        Provider();
        Expression<Func<Requete, string>> acces = r => r.NomDuChasseur;

        var nom = ValidatorOptions.Global.DisplayNameResolver(
            typeof(Requete), ((MemberExpression)acces.Body).Member, acces);

        nom.Should().Be("NomDuChasseur");
    }

    [Fact]
    public void Formule_en_francais_les_gabarits_de_message_par_defaut()
    {
        Provider();

        var resultat = new ValidatorDeRequete().Validate(new Requete());

        resultat.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("'NomDuChasseur' ne doit pas être vide.");
    }

    private sealed class ValidatorDeRequete : AbstractValidator<Requete>
    {
        public ValidatorDeRequete() => RuleFor(r => r.NomDuChasseur).NotEmpty();
    }
}
