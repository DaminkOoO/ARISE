using Arise.Domain.Quests;

namespace Arise.Application.Common.Abstractions;

/// <summary>
/// Accès aux quêtes posées. Implémenté dans la couche Infrastructure : la couche Application
/// ignore qu'il y a un PostgreSQL derrière.
///
/// <para>Vit dans <c>Common/Abstractions</c> et non sous <c>Features/Sport</c> : les quêtes ne
/// sont pas propres au Sport, les quatre domaines poseront les leurs dans la même table.</para>
/// </summary>
public interface IQuestRepository
{
    /// <summary>
    /// La quête de ce Chasseur, dans ce domaine, à cette date — ou <c>null</c> s'il n'y en a
    /// pas encore. <paramref name="questDate"/> est une date déjà exprimée dans le fuseau du
    /// Chasseur : le repository ne convertit rien.
    /// </summary>
    Task<Quest?> GetForDayAsync(
        Guid hunterProfileId,
        QuestDomain domain,
        DateOnly questDate,
        CancellationToken cancellationToken);

    /// <summary>
    /// La quête portant cet identifiant, ou <c>null</c> si aucune. C'est le chemin de la
    /// complétion : le Chasseur tape sur la quête qu'il a sous les yeux, dont l'écran connaît
    /// l'identifiant et non le jour ni le domaine.
    ///
    /// <para>Ne filtre pas sur le Chasseur : le rattachement se vérifie dans le handler, qui
    /// peut alors distinguer « quête inconnue » de « quête d'un autre » — et choisir de ne pas
    /// distinguer les deux dans ce qu'il rend.</para>
    /// </summary>
    Task<Quest?> GetByIdAsync(Guid questId, CancellationToken cancellationToken);

    Task SaveAsync(Quest quest, CancellationToken cancellationToken);
}
