using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Domain.Quests;
using MediatR;

namespace Arise.Application.Features.Sport.Queries.GetTodayGymQuest;

/// <summary>
/// Rend la quête de sport du jour d'un Chasseur, en la générant au premier passage de la
/// journée puis en la relisant les fois suivantes.
///
/// <para><b>Une requête qui écrit, et c'est assumé.</b> La règle CQRS du dépôt veut qu'une
/// requête ne mute rien ; celle-ci pose une ligne quand la quête du jour n'existe pas encore.
/// L'écriture n'est pas l'intention du Chasseur — il demande sa quête — mais la conséquence
/// d'une génération paresseuse : le texte doit être figé au premier affichage, sans quoi
/// chaque rafraîchissement rappellerait le Système et rendrait une quête différente. La seule
/// alternative serait une commande de génération déclenchée par le <c>briefing-worker</c> avant
/// la première consultation ; elle n'existe pas encore, et laisserait de toute façon ce chemin
/// nécessaire pour un Chasseur qui ouvre l'app avant le passage du worker.</para>
/// </summary>
public sealed class GetTodayGymQuestQueryHandler(
    IHunterProfileRepository hunterProfiles,
    IQuestRepository quests,
    IQuestGenerationAgent questGenerationAgent,
    TimeProvider timeProvider)
    : IRequestHandler<GetTodayGymQuestQuery, GetTodayGymQuestResult>
{
    public async Task<GetTodayGymQuestResult> Handle(
        GetTodayGymQuestQuery request, CancellationToken cancellationToken)
    {
        var aujourdhui = AujourdhuiChezLeChasseur(request.FuseauHoraire);

        // Une seule génération par jour : la quête lue le matin est celle qu'on retrouve le
        // soir, texte compris. On regarde donc d'abord si elle est déjà posée, avant même de
        // charger le profil — le chemin le plus fréquent ne coûte qu'une lecture.
        var dejaPosee = await quests.GetForDayAsync(
            request.HunterProfileId, QuestDomain.Sport, aujourdhui, cancellationToken);

        if (dejaPosee is not null)
        {
            return Vers(dejaPosee);
        }

        var profil = await hunterProfiles.GetByIdAsync(request.HunterProfileId, cancellationToken)
            ?? throw new HunterProfileNotFoundException();

        var generee = await questGenerationAgent.ExecuteAsync(
            new QuestGenerationAgentRequest(profil.Level, profil.Rank, profil.StreakCurrent),
            cancellationToken);

        var quete = Quest.Generate(
            profil.Id,
            QuestDomain.Sport,
            aujourdhui,
            generee.Title,
            generee.Description,
            generee.Type,
            generee.StatTarget,
            generee.Difficulty,
            generee.XpReward,
            generee.EstRepli);

        await quests.SaveAsync(quete, cancellationToken);

        return Vers(quete);
    }

    /// <summary>
    /// Le jour tel que le Chasseur le vit, pas celui du serveur : à 22h30 UTC, il est déjà
    /// demain à Paris et encore hier à New York. Générer sur la date UTC donnerait deux quêtes
    /// dans la même journée du Chasseur, ou aucune.
    ///
    /// <para>Le fuseau vient de la requête tant que <c>HunterProfile</c> ne le porte pas ; sa
    /// validité est contrôlée en amont par le validator de cette requête.</para>
    /// </summary>
    private DateOnly AujourdhuiChezLeChasseur(string fuseauHoraire)
    {
        var fuseau = TimeZoneInfo.FindSystemTimeZoneById(fuseauHoraire);

        return DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), fuseau).DateTime);
    }

    private static GetTodayGymQuestResult Vers(Quest quete) => new(
        quete.Id,
        quete.QuestDate,
        quete.Title,
        quete.Description,
        quete.Type,
        quete.StatTarget,
        quete.Difficulty,
        quete.XpReward,
        quete.IsCompleted,
        quete.IsFallback);
}
