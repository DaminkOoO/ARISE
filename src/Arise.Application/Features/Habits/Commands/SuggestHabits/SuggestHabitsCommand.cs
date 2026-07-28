using Arise.Application.Common.Messaging;

namespace Arise.Application.Features.Habits.Commands.SuggestHabits;

/// <summary>
/// Le Chasseur demande au Système de lui proposer des habitudes.
///
/// <para><b>Commande, bien qu'elle n'écrive rien en base</b>, et c'est délibéré : elle appelle
/// un système externe, ce qui coûte du temps et de l'argent et n'est donc ni gratuit ni
/// rejouable à volonté comme l'est une lecture. La ranger en requête inviterait à la déclencher
/// à chaque affichage d'écran. Le prix assumé est un <c>BEGIN</c>/<c>COMMIT</c> ouvert pour rien
/// par <c>TransactionBehavior</c>.</para>
///
/// <para>Elle ne déclare aucune habitude : le Chasseur choisit ensuite ce qu'il retient, et
/// c'est <c>CreateHabitCommand</c> qui écrit. Une commande qui créerait les habitudes d'office
/// remplirait sa liste de choses qu'il n'a pas voulues.</para>
/// </summary>
public sealed record SuggestHabitsCommand(Guid HunterProfileId) : ICommand<SuggestHabitsResult>;

/// <summary>
/// Ce que la suggestion rend à l'écran.
/// </summary>
/// <param name="EstRepli">
/// <see langword="true"/> quand le Système n'a rien rendu d'utilisable et que la liste est
/// générique. L'écran peut alors le dire franchement plutôt que de faire passer un repli pour
/// une suggestion sur mesure.
/// </param>
public sealed record SuggestHabitsResult(
    IReadOnlyList<HabitSuggestion> Suggestions,
    bool EstRepli);
