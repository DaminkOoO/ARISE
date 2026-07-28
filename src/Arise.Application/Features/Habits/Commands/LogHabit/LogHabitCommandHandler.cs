using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Domain.Habits;
using MediatR;

namespace Arise.Application.Features.Habits.Commands.LogHabit;

/// <summary>
/// Journalise une habitude tenue et rend la série recalculée.
///
/// <para><b>Aucun XP accordé, aucun événement publié.</b> Ce n'est pas un oubli : le document de
/// mécaniques ne chiffre de récompense que pour les quêtes, et la série d'engagement du profil
/// se nourrit d'un <c>QuestCompletedEvent</c>, que tenir une habitude ne produit pas
/// (section 2 : « ceci est distinct des séries par habitude individuelle »). Inventer ici un
/// barème que le document ne fixe pas déséquilibrerait la progression sans que personne n'ait
/// tranché — c'est une décision de conception, pas une valeur à improviser dans un handler.</para>
///
/// <para>La série n'est ni stockée ni incrémentée : elle est <b>recalculée</b> depuis le journal
/// par <see cref="SerieDHabitude"/>. Un compteur entretenu ici divergerait de son journal à la
/// première écriture concurrente, et plus rien ne dirait lequel des deux a raison.</para>
/// </summary>
public sealed class LogHabitCommandHandler(
    IHabitRepository habits,
    IHabitLogRepository habitLogs,
    TimeProvider timeProvider)
    : IRequestHandler<LogHabitCommand, LogHabitResult>
{
    public async Task<LogHabitResult> Handle(
        LogHabitCommand request, CancellationToken cancellationToken)
    {
        var habitude = await habits.GetByIdAsync(request.HabitId, cancellationToken)
            ?? throw new HabitNotFoundException();

        // Le rattachement annoncé n'est pas une étiquette de routage : sans ce contrôle,
        // n'importe quel Chasseur alimenterait la série des habitudes d'autrui. Même exception
        // que pour une habitude inconnue, pour ne pas révéler celle d'un autre.
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

        var jour = JourDuChasseur(request.FuseauHoraire);

        // Une seule lecture sert les deux besoins : savoir si le jour est déjà tenu, et calculer
        // la série. Les redemander séparément doublerait l'aller-retour pour la même réponse.
        var joursTenus = await habitLogs.GetDaysAsync(habitude.Id, cancellationToken);

        // Double-tap, renvoi réseau, deux appareils : le jour est déjà tenu et le journal n'a pas
        // à recevoir une seconde ligne. Ce n'est pas une erreur — l'habitude est tenue, et c'est
        // le drapeau, pas une exception, qui le dit à l'écran.
        if (joursTenus.Contains(jour))
        {
            return Resultat(habitude, jour, joursTenus, dejaJournalisee: true);
        }

        var joursApres = joursTenus.Append(jour).ToList();

        try
        {
            await habitLogs.AddAsync(
                HabitLog.Create(habitude.Id, jour, timeProvider.GetUtcNow()), cancellationToken);
        }
        // Deux taps simultanés, deux scopes, deux DbContext : la lecture ci-dessus n'a rien pu
        // voir, et c'est l'index unique du journal qui tranche. Le perdant se comporte alors
        // exactement comme un double-tap séquentiel.
        catch (HabitAlreadyLoggedException)
        {
            return Resultat(habitude, jour, joursApres, dejaJournalisee: true);
        }

        return Resultat(habitude, jour, joursApres, dejaJournalisee: false);
    }

    /// <summary>
    /// Le jour tel que le Chasseur le vit, comme pour la génération et la complétion d'une quête :
    /// c'est à ce jour-là que l'effort appartient, et non à celui du serveur. Le cas qui tranche
    /// (doc mécaniques, section 2) : il est 22h30 en UTC, donc déjà demain à Paris.
    /// </summary>
    private DateOnly JourDuChasseur(string fuseauHoraire) =>
        DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(
                timeProvider.GetUtcNow(),
                TimeZoneInfo.FindSystemTimeZoneById(fuseauHoraire))
                .DateTime);

    /// <summary>
    /// La série est recomptée sur le journal <b>tel qu'il est après cet appel</b> : l'écran
    /// affiche « 3 jours » dès le tap, sans avoir à relire.
    /// </summary>
    private static LogHabitResult Resultat(
        Habit habitude,
        DateOnly jour,
        IReadOnlyList<DateOnly> joursTenus,
        bool dejaJournalisee) =>
        new(
            habitude.Id,
            jour,
            dejaJournalisee,
            SerieDHabitude.Calculer(habitude.Frequency, joursTenus, jour));
}
