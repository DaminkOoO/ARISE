using Arise.Application.Common.Messaging;
using Arise.Application.Features.Sport.Queries.GetTodayGymQuest;
using FluentAssertions;
using MediatR;

namespace Arise.Application.Tests.Common.Messaging;

/// <summary>
/// Le tri commande/requête est un fait de structure, pas une convention de nommage : c'est lui
/// qui décide quels messages traversent le <c>TransactionBehavior</c>. Un message oublié se
/// rangerait silencieusement du mauvais côté — une écriture sans transaction, ou une lecture
/// qui en ouvre une pour rien.
///
/// <para>Ces tests balaient donc l'assembly entière plutôt que d'énumérer les types connus :
/// c'est le seul moyen qu'une commande ajoutée en Phase 3 sans marqueur fasse rougir la suite
/// au lieu de passer inaperçue.</para>
/// </summary>
public class MarqueursDeMessagesTests
{
    /// <summary>
    /// Tous les messages MediatR concrets de la couche Application — publics comme internes
    /// (la sonde de pipeline l'est), puisque tous traversent le même pipeline.
    /// </summary>
    private static IEnumerable<Type> MessagesDeLAssembly() =>
        DependencyInjection.AssemblyApplication.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => Porte(type, typeof(IRequest<>)));

    private static bool Porte(Type type, Type marqueurGenerique) =>
        type.GetInterfaces().Any(interfaceImplementee =>
            interfaceImplementee.IsGenericType
            && interfaceImplementee.GetGenericTypeDefinition() == marqueurGenerique);

    [Fact]
    public void Tout_message_MediatR_de_l_assembly_porte_ICommand_ou_IQuery()
    {
        var sansMarqueur = MessagesDeLAssembly()
            .Where(type => !Porte(type, typeof(ICommand<>)) && !Porte(type, typeof(IQuery<>)))
            .Select(type => type.FullName)
            .ToList();

        sansMarqueur.Should().BeEmpty(
            "chaque message doit se ranger explicitement du côté écriture ou du côté lecture");
    }

    // Les deux marqueurs décident de comportements opposés dans le pipeline : un message qui
    // porterait les deux serait à la fois enveloppé dans une transaction et supposé n'en pas
    // avoir besoin. Mieux vaut que la suite le refuse que de deviner lequel l'emporte.
    [Fact]
    public void Aucun_message_ne_porte_les_deux_marqueurs_a_la_fois()
    {
        var ambigus = MessagesDeLAssembly()
            .Where(type => Porte(type, typeof(ICommand<>)) && Porte(type, typeof(IQuery<>)))
            .Select(type => type.FullName)
            .ToList();

        ambigus.Should().BeEmpty();
    }

    /// <summary>
    /// La déviation assumée du dépôt, épinglée ici plutôt que laissée à un commentaire :
    /// <see cref="GetTodayGymQuestQuery"/> est une lecture qui <b>déclenche</b> une écriture —
    /// elle envoie <c>GenerateTodayQuestCommand</c> quand aucune quête n'est encore posée.
    ///
    /// <para>Elle reste une requête, et c'est la commande interne qui porte la transaction :
    /// c'est le découpage juste. La génération est l'unité atomique — agent interrogé, quête
    /// posée — et elle doit être tout ou rien indépendamment de qui l'a demandée (la lecture
    /// aujourd'hui, le <c>briefing-worker</c> demain, qui ne passera pas par cette requête).
    /// Classer la requête en commande étendrait la transaction à la projection d'affichage qui
    /// suit, et surtout à l'appel réseau au Système — des secondes d'attente pendant lesquelles
    /// une transaction Postgres resterait ouverte sur rien.</para>
    /// </summary>
    [Fact]
    public void GetTodayGymQuestQuery_reste_une_requete_malgre_l_ecriture_qu_elle_declenche()
    {
        Porte(typeof(GetTodayGymQuestQuery), typeof(IQuery<>)).Should().BeTrue();
    }
}
