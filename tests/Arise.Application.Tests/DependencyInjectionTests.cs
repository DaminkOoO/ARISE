using System.Linq.Expressions;
using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Behaviors;
using Arise.Application.Common.Diagnostics;
using Arise.Application.Common.Messaging;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Arise.Application.Tests;

public class DependencyInjectionTests
{
    public sealed record RequeteFactice(string Nom) : IQuery<string>;

    public sealed record CommandeFactice(string Nom) : ICommand<string>;

    private static ServiceProvider Provider() =>
        new ServiceCollection().AddApplication().BuildServiceProvider();

    /// <summary>
    /// Ce que l'hôte réel monte : la couche Application par-dessus une unité de travail, que
    /// seule la couche Infrastructure sait fournir. <c>AddApplication()</c> nu suffit à tout ce
    /// qui ne touche pas de commande — d'où la doublure ici plutôt qu'une référence à
    /// Infrastructure, que ce projet de tests n'a pas et n'a pas à avoir.
    /// </summary>
    private static ServiceProvider ProviderAvecUniteDeTravail() =>
        new ServiceCollection()
            .AddSingleton<IUnitOfWork, UniteDeTravailInerte>()
            .AddApplication()
            .BuildServiceProvider();

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

    // Les handlers datent ce qu'ils écrivent depuis une TimeProvider injectée, pour rester
    // testables à horloge figée. Sans enregistrement par défaut, RegisterUserCommandHandler
    // ne se construit pas — et l'échec n'apparaît qu'à la première requête reçue.
    [Fact]
    public void Fournit_une_horloge_par_defaut()
    {
        Provider().GetService<TimeProvider>().Should().Be(TimeProvider.System);
    }

    // L'hôte doit pouvoir figer l'horloge — un test de bout en bout sur une série de jours
    // ne peut pas attendre minuit.
    [Fact]
    public void Laisse_l_hote_substituer_son_horloge()
    {
        var horloge = new HorlogeFigee();

        using var provider = new ServiceCollection()
            .AddSingleton<TimeProvider>(horloge)
            .AddApplication()
            .BuildServiceProvider();

        provider.GetService<TimeProvider>().Should().BeSameAs(horloge);
    }

    private sealed class HorlogeFigee : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 7, 23, 9, 0, 0, TimeSpan.Zero);
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

    // Une commande écrit : elle traverse le TransactionBehavior, sans quoi une commande ajoutée
    // en Phase 3 écrirait de nouveau en trois transactions indépendantes.
    [Fact]
    public void Branche_le_TransactionBehavior_sur_une_commande()
    {
        var behaviors = ProviderAvecUniteDeTravail()
            .GetServices<IPipelineBehavior<CommandeFactice, string>>();

        behaviors.Should().ContainItemsAssignableTo<TransactionBehavior<CommandeFactice, string>>();
    }

    // Le pendant, et la raison d'être des marqueurs : une lecture ne doit pas payer un
    // BEGIN/COMMIT par affichage.
    //
    // L'assertion passe par le type ouvert et non par TransactionBehavior<RequeteFactice, string>
    // — que le compilateur refuse d'écrire, la contrainte n'étant pas satisfaite. C'est en soi
    // la meilleure nouvelle possible : le mauvais rangement est impossible à exprimer. Reste à
    // vérifier que le conteneur écarte bien la fermeture impossible au lieu de lever à la
    // résolution, ce qu'aucun compilateur ne dit.
    [Fact]
    public void N_ouvre_pas_de_transaction_sur_une_requete()
    {
        var behaviors = ProviderAvecUniteDeTravail()
            .GetServices<IPipelineBehavior<RequeteFactice, string>>();

        behaviors.Should().NotContain(behavior =>
            behavior.GetType().IsGenericType
            && behavior.GetType().GetGenericTypeDefinition() == typeof(TransactionBehavior<,>));
    }

    // L'ordre compte : une commande refusée par ses validators ne doit jamais avoir ouvert de
    // transaction. MediatR exécute les behaviors dans l'ordre d'enregistrement — c'est donc
    // celui-là qu'on épingle.
    [Fact]
    public void Valide_la_commande_avant_d_ouvrir_la_transaction()
    {
        var behaviors = ProviderAvecUniteDeTravail()
            .GetServices<IPipelineBehavior<CommandeFactice, string>>()
            .ToList();

        behaviors.Should().SatisfyRespectively(
            premier => premier.Should().BeOfType<ValidationBehavior<CommandeFactice, string>>(),
            second => second.Should().BeOfType<TransactionBehavior<CommandeFactice, string>>());
    }

    [Fact]
    public void N_enregistre_le_TransactionBehavior_qu_une_fois_si_appele_deux_fois()
    {
        using var provider = new ServiceCollection()
            .AddSingleton<IUnitOfWork, UniteDeTravailInerte>()
            .AddApplication()
            .AddApplication()
            .BuildServiceProvider();

        provider.GetServices<IPipelineBehavior<CommandeFactice, string>>()
            .OfType<TransactionBehavior<CommandeFactice, string>>()
            .Should().ContainSingle();
    }

    /// <summary>
    /// Doublure sans effet : ces tests n'observent que le câblage — ce que le behavior fait de
    /// l'unité de travail est éprouvé à part (<c>TransactionBehaviorTests</c>).
    /// </summary>
    private sealed class UniteDeTravailInerte : IUnitOfWork
    {
        public bool TransactionEnCours => false;

        public Task CommencerAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ValiderAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AnnulerAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
