using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Application.Common.Validation;
using Arise.Application.Features.Hunters.Commands.AwardXp;
using Arise.Domain.Habits;
using Arise.Domain.Hunters;
using MediatR;

namespace Arise.Application.Features.Habits.Commands.LogHabit;

/// <summary>
/// Journalise une habitude tenue, accorde l'XP d'engagement, et rend la série recalculée.
///
/// <para>L'XP passe par <see cref="AwardXpCommand"/> via MediatR, jamais par un appel direct à
/// <c>HunterProfile.AwardXp</c> : le moteur de progression a un point d'entrée unique, et
/// l'appeler en direct le priverait de sa validation et de son pipeline. Le montant vient de
/// <see cref="BaremeXpEngagement"/> — fixe, et plafonné sur la journée du Chasseur.</para>
///
/// <para>Aucun événement de domaine n'est publié : la série d'engagement du profil ne se nourrit
/// que de <c>QuestCompletedEvent</c> (doc mécaniques, section 2), et tenir une habitude n'est pas
/// compléter une quête. La série d'habitude, elle, est <b>recalculée</b> depuis le journal par
/// <see cref="SerieDHabitude"/> : un compteur entretenu ici divergerait de son journal à la
/// première écriture concurrente.</para>
/// </summary>
public sealed class LogHabitCommandHandler(
    IHabitRepository habits,
    IHabitLogRepository habitLogs,
    ITaskItemRepository tasks,
    ISender sender,
    TimeProvider timeProvider)
    : IRequestHandler<LogHabitCommand, LogHabitResult>
{
    public async Task<LogHabitResult> Handle(
        LogHabitCommand request, CancellationToken cancellationToken)
    {
        var habitude = await habits.GetByIdAsync(request.HabitId, cancellationToken)
            ?? throw new HabitNotFoundException();

        // Le rattachement annoncé n'est pas une étiquette de routage : sans ce contrôle,
        // n'importe quel Chasseur alimenterait la série des habitudes d'autrui — et
        // s'accorderait leur XP. Même exception que pour une habitude inconnue, pour ne pas
        // révéler celle d'un autre.
        if (habitude.HunterProfileId != request.HunterProfileId)
        {
            throw new HabitNotFoundException();
        }

        // Une habitude rangée a quitté la liste : la journaliser ne peut venir que d'un écran
        // resté ouvert. Message distinct de l'introuvable, parce que celle-ci, le Chasseur peut
        // la reprendre — et c'est ce qu'il faut lui dire.
        if (habitude.IsArchived)
        {
            throw new HabitArchivedException();
        }

        var jour = JourDuChasseur.Aujourdhui(timeProvider, request.FuseauHoraire);

        // Une seule lecture sert les deux besoins : savoir si le jour est déjà tenu, et calculer
        // la série. Les redemander séparément doublerait l'aller-retour pour la même réponse.
        var joursTenus = await habitLogs.GetDaysAsync(habitude.Id, cancellationToken);

        // Double-tap, renvoi réseau, deux appareils : le jour est déjà tenu et le journal n'a pas
        // à recevoir une seconde ligne. Ce n'est pas une erreur — l'habitude est tenue, et c'est
        // le drapeau, pas une exception, qui le dit à l'écran. Aucun XP : il a été accordé au
        // premier appel.
        if (joursTenus.Contains(jour))
        {
            return Resultat(habitude, jour, joursTenus, dejaJournalisee: true, xpAcquis: 0);
        }

        // Compté avant l'écriture, donc sans le geste en cours : c'est bien « ce qui a déjà été
        // acquis » qu'il faut opposer au plafond.
        var dejaAcquis = await XpDEngagementDejaAcquis(
            request.HunterProfileId, jour, request.FuseauHoraire, cancellationToken);

        var gain = BaremeXpEngagement.Accordable(
            BaremeXpEngagement.PourHabitude(habitude.Frequency), dejaAcquis);

        var joursApres = joursTenus.Append(jour).ToList();

        try
        {
            await habitLogs.AddAsync(
                HabitLog.Create(habitude.Id, jour, timeProvider.GetUtcNow()), cancellationToken);
        }
        // Deux taps simultanés, deux scopes, deux DbContext : la lecture ci-dessus n'a rien pu
        // voir, et c'est l'index unique du journal qui tranche. Le perdant se comporte alors
        // exactement comme un double-tap séquentiel — et n'accorde donc aucun XP, que le gagnant
        // a déjà crédité.
        catch (HabitAlreadyLoggedException)
        {
            return Resultat(habitude, jour, joursApres, dejaJournalisee: true, xpAcquis: 0);
        }

        // Après la persistance : si le processus tombait entre les deux, le Chasseur perdrait un
        // gain — pas la garde qui l'empêche d'être accordé deux fois à la reprise. Des deux
        // pannes, c'est la seule qui se rattrape.
        if (gain > 0)
        {
            await sender.Send(new AwardXpCommand(habitude.HunterProfileId, gain), cancellationToken);
        }

        return Resultat(habitude, jour, joursApres, dejaJournalisee: false, xpAcquis: gain);
    }

    /// <summary>
    /// L'XP d'engagement déjà acquis sur la journée du Chasseur, recalculé depuis ses gestes —
    /// habitudes tenues et tâches cochées confondues, le plafond étant cumulé entre les deux
    /// domaines.
    /// </summary>
    private async Task<int> XpDEngagementDejaAcquis(
        Guid hunterProfileId,
        DateOnly jour,
        string fuseauHoraire,
        CancellationToken cancellationToken)
    {
        var habitudesTenues = await habitLogs.GetDayFrequenciesForHunterAsync(
            hunterProfileId, jour, cancellationToken);

        var (debut, fin) = JourDuChasseur.FenetreUtc(jour, fuseauHoraire);

        var tachesCochees = await tasks.CountCompletedBetweenAsync(
            hunterProfileId, debut, fin, cancellationToken);

        return BaremeXpEngagement.TotalDuJour(habitudesTenues, tachesCochees);
    }

    /// <summary>
    /// La série est recomptée sur le journal <b>tel qu'il est après cet appel</b> : l'écran
    /// affiche « 3 jours » dès le tap, sans avoir à relire.
    /// </summary>
    private static LogHabitResult Resultat(
        Habit habitude,
        DateOnly jour,
        IReadOnlyList<DateOnly> joursTenus,
        bool dejaJournalisee,
        int xpAcquis) =>
        new(
            habitude.Id,
            jour,
            dejaJournalisee,
            SerieDHabitude.Calculer(habitude.Frequency, joursTenus, jour),
            xpAcquis);
}
