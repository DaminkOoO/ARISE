using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Behaviors;
using Arise.Application.Common.Messaging;
using FluentAssertions;
using MediatR;

namespace Arise.Application.Tests.Common.Behaviors;

/// <summary>
/// Le behavior qui rend une commande atomique. Ce qu'il doit tenir tient en une phrase : la
/// commande la <b>plus externe</b> ouvre et valide la transaction, les commandes qu'elle envoie
/// à leur tour rejoignent celle en cours.
///
/// <para>C'est la partie subtile : <c>CompleteGymQuestCommand</c> envoie
/// <c>AwardXpCommand</c> par MediatR. Un behavior naïf ouvrirait une transaction par commande,
/// et l'interne validerait avant la fin de l'externe — l'XP serait committé pendant que la
/// complétion de la quête pourrait encore échouer, soit exactement le trou qu'on rebouche.</para>
/// </summary>
public class TransactionBehaviorTests
{
    private sealed record CommandeFactice : ICommand<string>;

    private const string Reponse = "commande traversée";

    private static TransactionBehavior<CommandeFactice, string> Behavior(IUnitOfWork uniteDeTravail) =>
        new(uniteDeTravail);

    private static Task<string> Aboutit(CancellationToken _) => Task.FromResult(Reponse);

    private static Task<string> Echoue(CancellationToken _) =>
        Task.FromException<string>(new InvalidOperationException("panne du handler"));

    [Fact]
    public async Task Valide_la_transaction_quand_la_commande_aboutit()
    {
        var uniteDeTravail = new UniteDeTravailEspionne();

        await Behavior(uniteDeTravail).Handle(new CommandeFactice(), Aboutit, CancellationToken.None);

        uniteDeTravail.Appels.Should().Equal("commencer", "valider");
    }

    [Fact]
    public async Task Rend_le_resultat_du_handler_sans_le_toucher()
    {
        var resultat = await Behavior(new UniteDeTravailEspionne())
            .Handle(new CommandeFactice(), Aboutit, CancellationToken.None);

        resultat.Should().Be(Reponse);
    }

    // Le cœur de l'affaire : sans annulation, une commande tombée à mi-parcours laisserait ses
    // premières écritures derrière elle — la quête complétée sans son XP.
    [Fact]
    public async Task Annule_la_transaction_quand_la_commande_leve()
    {
        var uniteDeTravail = new UniteDeTravailEspionne();

        var acte = async () => await Behavior(uniteDeTravail)
            .Handle(new CommandeFactice(), Echoue, CancellationToken.None);

        await acte.Should().ThrowAsync<InvalidOperationException>();
        uniteDeTravail.Appels.Should().Equal("commencer", "annuler");
    }

    // L'annulation ne masque pas la panne : l'appelant doit toujours voir ce qui a échoué, et
    // le bord HTTP doit toujours pouvoir le traduire.
    [Fact]
    public async Task Laisse_la_panne_remonter_apres_avoir_annule()
    {
        var acte = async () => await Behavior(new UniteDeTravailEspionne())
            .Handle(new CommandeFactice(), Echoue, CancellationToken.None);

        (await acte.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("panne du handler");
    }

    /// <summary>
    /// L'imbrication, jouée telle qu'elle se produit : deux instances de behavior — MediatR en
    /// construit une par envoi — partagent la <b>même</b> unité de travail, qui vit à l'échelle
    /// du scope de la requête.
    /// </summary>
    private async Task<UniteDeTravailEspionne> CommandeImbriquee(
        RequestHandlerDelegate<string> interne)
    {
        var uniteDeTravail = new UniteDeTravailEspionne();

        var acte = async () => await Behavior(uniteDeTravail).Handle(
            new CommandeFactice(),
            _ => Behavior(uniteDeTravail).Handle(new CommandeFactice(), interne, CancellationToken.None),
            CancellationToken.None);

        try
        {
            await acte();
        }
        catch (InvalidOperationException)
        {
            // La panne interne est le sujet des tests qui l'attendent ; ici on n'observe que
            // les appels reçus par l'unité de travail.
        }

        return uniteDeTravail;
    }

    [Fact]
    public async Task N_ouvre_qu_une_transaction_pour_une_commande_imbriquee()
    {
        var uniteDeTravail = await CommandeImbriquee(Aboutit);

        uniteDeTravail.Appels.Count(appel => appel == "commencer").Should().Be(1);
    }

    // Le piège précis : si la commande interne validait, l'XP serait acquis pendant que la
    // commande externe peut encore échouer — et la panne suivante ne pourrait plus le reprendre.
    [Fact]
    public async Task Ne_valide_pas_depuis_la_commande_imbriquee()
    {
        var uniteDeTravail = await CommandeImbriquee(Aboutit);

        uniteDeTravail.Appels.Should().Equal("commencer", "valider");
    }

    // Le symétrique : une commande interne qui tombe ne doit pas annuler la transaction sous les
    // pieds de l'externe — c'est à celle-ci, seule à savoir qu'elle est la plus externe, de le
    // faire une fois la panne remontée jusqu'à elle.
    [Fact]
    public async Task N_annule_qu_une_fois_quand_la_commande_imbriquee_leve()
    {
        var uniteDeTravail = await CommandeImbriquee(Echoue);

        uniteDeTravail.Appels.Should().Equal("commencer", "annuler");
    }

    /// <summary>
    /// Tient l'ordre des appels reçus plutôt que de simples compteurs : « a validé » et « a
    /// validé <b>après</b> avoir ouvert » ne sont pas la même promesse, et c'est la seconde qui
    /// nous intéresse.
    /// </summary>
    private sealed class UniteDeTravailEspionne : IUnitOfWork
    {
        public List<string> Appels { get; } = [];

        public bool TransactionEnCours { get; private set; }

        public Task CommencerAsync(CancellationToken cancellationToken)
        {
            Appels.Add("commencer");
            TransactionEnCours = true;

            return Task.CompletedTask;
        }

        public Task ValiderAsync(CancellationToken cancellationToken)
        {
            Appels.Add("valider");
            TransactionEnCours = false;

            return Task.CompletedTask;
        }

        public Task AnnulerAsync(CancellationToken cancellationToken)
        {
            Appels.Add("annuler");
            TransactionEnCours = false;

            return Task.CompletedTask;
        }
    }
}
