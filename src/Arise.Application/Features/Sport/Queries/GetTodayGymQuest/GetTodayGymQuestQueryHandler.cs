using Arise.Application.Common.Abstractions;
using Arise.Application.Features.Sport.Commands.GenerateTodayQuest;
using Arise.Domain.Quests;
using MediatR;

namespace Arise.Application.Features.Sport.Queries.GetTodayGymQuest;

/// <summary>
/// Rend la quête de sport du jour d'un Chasseur : celle qui est déjà posée, ou celle que
/// <see cref="GenerateTodayQuestCommand"/> vient d'écrire s'il n'y en a pas encore.
///
/// <para>La génération est paresseuse parce que le texte doit être figé au premier affichage :
/// sans cela, chaque rafraîchissement rappellerait le Système et rendrait une quête différente.
/// Elle passe par MediatR et non par un appel direct au handler d'écriture — la commande garde
/// ainsi sa validation et son pipeline — et le chemin reste nécessaire même une fois le
/// <c>briefing-worker</c> en place, pour le Chasseur qui ouvre l'app avant son passage.</para>
/// </summary>
public sealed class GetTodayGymQuestQueryHandler(
    IQuestRepository quests,
    ISender sender,
    TimeProvider timeProvider)
    : IRequestHandler<GetTodayGymQuestQuery, GetTodayGymQuestResult>
{
    public async Task<GetTodayGymQuestResult> Handle(
        GetTodayGymQuestQuery request, CancellationToken cancellationToken)
    {
        var aujourdhui = AujourdhuiChezLeChasseur(request.FuseauHoraire);

        // Une seule génération par jour : la quête lue le matin est celle qu'on retrouve le
        // soir, texte compris. Le chemin le plus fréquent ne coûte donc qu'une lecture.
        var dejaPosee = await quests.GetForDayAsync(
            request.HunterProfileId, QuestDomain.Sport, aujourdhui, cancellationToken);

        if (dejaPosee is not null)
        {
            return Vers(dejaPosee);
        }

        var generee = await sender.Send(
            new GenerateTodayQuestCommand(request.HunterProfileId, aujourdhui), cancellationToken);

        return Vers(generee);
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
