using Arise.Application.Common.Messaging;

namespace Arise.Application.Features.Habits.Commands.LogHabit;

/// <summary>
/// Le Chasseur déclare avoir tenu une habitude.
///
/// <para>Le fuseau voyage dans la commande, comme dans la complétion d'une quête et pour la même
/// raison : c'est lui qui décide du jour auquel l'effort est daté, donc de ce que la série
/// comptera. Il disparaîtra de toutes le jour où <c>HunterProfile</c> le portera.</para>
///
/// <para><paramref name="HunterProfileId"/> est annoncé par l'appelant tant que le rattachement
/// au jeton d'authentification n'est pas posé — le handler vérifie néanmoins que l'habitude lui
/// appartient, sans quoi n'importe quel Chasseur alimenterait la série des habitudes d'autrui.
/// </para>
/// </summary>
public sealed record LogHabitCommand(
    Guid HunterProfileId,
    Guid HabitId,
    string FuseauHoraire) : ICommand<LogHabitResult>;

/// <summary>
/// Ce que la journalisation rend à l'écran : de quoi confirmer le geste et afficher la série
/// sans relire la liste.
/// </summary>
/// <param name="Jour">
/// Le jour <b>du Chasseur</b> auquel l'effort a été daté — pas celui du serveur. Le rendre permet
/// à l'écran de constater qu'un tap passé minuit a bien crédité la veille, plutôt que de le
/// déduire d'une horloge locale qui pourrait différer.
/// </param>
/// <param name="DejaJournalisee">
/// <see langword="true"/> quand ce jour était déjà tenu avant cet appel — double-tap, renvoi
/// réseau, deux appareils. Ce n'est pas une erreur : l'habitude est tenue, et c'est ce drapeau
/// qui permet à l'écran d'écrire « déjà validée aujourd'hui » plutôt que de rejouer l'animation.
/// </param>
/// <param name="SerieActuelle">
/// La série de cette habitude — en jours pour une quotidienne, en semaines pour une hebdomadaire
/// — recomptée sur le journal tel qu'il est après cet appel. Locale à l'habitude : elle ne se
/// confond pas avec la série d'engagement du profil Chasseur.
/// </param>
public sealed record LogHabitResult(
    Guid HabitId,
    DateOnly Jour,
    bool DejaJournalisee,
    int SerieActuelle);
