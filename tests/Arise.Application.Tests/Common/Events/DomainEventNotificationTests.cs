using System.Runtime.CompilerServices;
using Arise.Application.Common.Events;
using Arise.Domain.Common;
using Arise.Domain.Hunters;
using Arise.Domain.Quests;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Arise.Application.Tests.Common.Events;

/// <summary>
/// Le pont entre un fait du Domain et une notification MediatR. Il est <b>générique</b> pour
/// qu'un abonné choisisse l'événement qui l'intéresse : avec une enveloppe unique, tout
/// <c>INotificationHandler</c> recevrait chaque fait du dépôt et devrait trancher par un test de
/// type dans son corps — le handler de série réagirait aux montées de rang.
/// </summary>
public class DomainEventNotificationTests
{
    private static readonly HunterRankedUpEvent MonteeDeRang =
        new(Guid.NewGuid(), HunterRank.E, HunterRank.D);

    private static readonly QuestCompletedEvent Completion = new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        QuestDomain.Sport,
        QuestType.Quotidienne,
        new DateOnly(2026, 7, 25));

    // L'appelant tient un IDomainEvent statiquement typé — les entités accumulent leurs faits
    // dans une même liste. Refermer le générique demande donc de résoudre le type à l'exécution,
    // et c'est le seul endroit du dépôt qui le fasse.
    [Fact]
    public void Referme_l_enveloppe_sur_le_type_reel_de_l_evenement()
    {
        IDomainEvent fait = MonteeDeRang;

        DomainEventNotification.Envelopper(fait)
            .Should().BeOfType<DomainEventNotification<HunterRankedUpEvent>>();
    }

    [Fact]
    public void Referme_l_enveloppe_sur_le_type_reel_d_un_autre_evenement()
    {
        IDomainEvent fait = Completion;

        DomainEventNotification.Envelopper(fait)
            .Should().BeOfType<DomainEventNotification<QuestCompletedEvent>>();
    }

    [Fact]
    public void Transporte_l_evenement_enveloppe()
    {
        DomainEventNotification.Envelopper(MonteeDeRang)
            .Should().BeOfType<DomainEventNotification<HunterRankedUpEvent>>()
            .Which.DomainEvent.Should().BeSameAs(MonteeDeRang);
    }

    // La couture qui porte tout le dispositif : l'enveloppe est publiée avec le type statique
    // INotification, et c'est MediatR qui doit router sur son type réel. S'il routait sur le
    // paramètre générique du site d'appel, aucun abonné ne serait jamais réveillé — et rien
    // d'autre ne rougirait, puisqu'une publication sans abonné est silencieuse.
    [Fact]
    public async Task Atteint_l_abonne_du_type_concret_a_travers_MediatR()
    {
        var abonne = new AbonneFactice();
        await using var provider = new ServiceCollection()
            .AddApplication()
            .AddSingleton<INotificationHandler<DomainEventNotification<FaitFactice>>>(abonne)
            .BuildServiceProvider();

        await provider.GetRequiredService<IPublisher>()
            .Publish(DomainEventNotification.Envelopper(new FaitFactice("levé")), CancellationToken.None);

        abonne.Recus.Should().ContainSingle().Which.Valeur.Should().Be("levé");
    }

    [Fact]
    public async Task N_atteint_pas_l_abonne_d_un_autre_type_d_evenement()
    {
        var abonne = new AbonneFactice();
        await using var provider = new ServiceCollection()
            .AddApplication()
            .AddSingleton<INotificationHandler<DomainEventNotification<FaitFactice>>>(abonne)
            .BuildServiceProvider();

        await provider.GetRequiredService<IPublisher>()
            .Publish(DomainEventNotification.Envelopper(MonteeDeRang), CancellationToken.None);

        abonne.Recus.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------------------
    // Balayage de tous les faits déclarés dans le Domain. La fabrique est le seul endroit du
    // dépôt qui résolve un type à l'exécution : une réflexion ne casse pas à la compilation
    // mais au runtime, et le mode de panne serait le pire possible — quête persistée, XP
    // crédité, exception au moment de publier. Déclarer un IDomainEvent suffit désormais à être
    // couvert ici : aucun futur événement n'a besoin qu'on pense à ajouter son test.
    // ---------------------------------------------------------------------------------------

    private static IEnumerable<Type> FaitsDuDomaine() =>
        typeof(IDomainEvent).Assembly.GetTypes()
            .Where(type => !type.IsAbstract
                && !type.IsInterface
                && typeof(IDomainEvent).IsAssignableFrom(type));

    public static TheoryData<Type> TousLesFaitsDuDomaine()
    {
        var faits = new TheoryData<Type>();

        foreach (var type in FaitsDuDomaine())
        {
            faits.Add(type);
        }

        return faits;
    }

    [Theory]
    [MemberData(nameof(TousLesFaitsDuDomaine))]
    public void Enveloppe_tout_fait_declare_dans_le_Domain(Type typeDuFait)
    {
        // Instancié sans passer par son constructeur : le balayage ne connaît pas les
        // composants du fait, et seul son type réel importe à la fabrique.
        var fait = (IDomainEvent)RuntimeHelpers.GetUninitializedObject(typeDuFait);

        DomainEventNotification.Envelopper(fait)
            .Should().BeOfType(typeof(DomainEventNotification<>).MakeGenericType(typeDuFait));
    }

    // Sans quoi une découverte cassée rendrait le balayage vide, donc vert pour rien.
    [Fact]
    public void Le_balayage_trouve_les_faits_connus_du_Domain()
    {
        FaitsDuDomaine().Should().Contain(
            [typeof(HunterRankedUpEvent), typeof(QuestCompletedEvent)]);
    }

    public sealed record FaitFactice(string Valeur) : IDomainEvent;

    private sealed class AbonneFactice : INotificationHandler<DomainEventNotification<FaitFactice>>
    {
        public List<FaitFactice> Recus { get; } = [];

        public Task Handle(
            DomainEventNotification<FaitFactice> notification, CancellationToken cancellationToken)
        {
            Recus.Add(notification.DomainEvent);
            return Task.CompletedTask;
        }
    }
}
