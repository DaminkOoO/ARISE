using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using MediatR;

namespace Arise.Application.Features.Habits.Commands.SuggestHabits;

/// <summary>
/// Fait proposer par le Système des habitudes que le Chasseur ne suit pas encore.
///
/// <para>Le filtrage des suggestions déjà suivies est fait <b>ici aussi</b>, alors que le prompt
/// le demande déjà : un garde-fou écrit uniquement dans le prompt n'en est pas un. Sans ce
/// filtre, une suggestion reproposée serait affichée, tapée par le Chasseur, puis refusée par
/// l'index unique des habitudes — une erreur pour un geste que le Système lui avait
/// suggéré.</para>
/// </summary>
public sealed class SuggestHabitsCommandHandler(
    IHunterProfileRepository hunterProfiles,
    IHabitRepository habits,
    IHabitSuggestionAgent habitSuggestionAgent)
    : IRequestHandler<SuggestHabitsCommand, SuggestHabitsResult>
{
    /// <summary>
    /// Insensible à la casse mais <b>pas</b> aux accents, exactement comme la collation
    /// <c>und-u-ks-level2</c> que porte la colonne du nom d'habitude. Comparer autrement
    /// donnerait un filtre applicatif qui écarte ce que la base aurait accepté — ou qui laisse
    /// passer ce qu'elle refusera.
    /// </summary>
    private static readonly StringComparer ComparaisonDesNoms =
        StringComparer.CurrentCultureIgnoreCase;

    public async Task<SuggestHabitsResult> Handle(
        SuggestHabitsCommand request, CancellationToken cancellationToken)
    {
        // Avant l'appel au Système, qui coûte du temps et de l'argent : le refuser au plus tôt
        // n'est pas une optimisation gratuite.
        var profil = await hunterProfiles.GetByIdAsync(request.HunterProfileId, cancellationToken)
            ?? throw new HunterProfileNotFoundException();

        var declarees = await habits.GetForHunterAsync(profil.Id, cancellationToken);

        // Les archivées n'entrent pas dans la liste : une habitude rangée n'est plus suivie, et
        // la reproposer est justement le service attendu.
        var dejaSuivies = declarees
            .Where(habitude => !habitude.IsArchived)
            .Select(habitude => habitude.Name)
            .ToList();

        var proposees = await habitSuggestionAgent.ExecuteAsync(
            new HabitSuggestionAgentRequest(profil.Level, profil.Rank, dejaSuivies),
            cancellationToken);

        var retenues = proposees.Suggestions
            .Where(suggestion => !dejaSuivies.Contains(suggestion.Name, ComparaisonDesNoms))
            // Le modèle se répète parfois à l'intérieur d'une même réponse ; deux lignes
            // identiques dans une liste de choix n'aident personne.
            .DistinctBy(suggestion => suggestion.Name, ComparaisonDesNoms)
            .ToList();

        return new SuggestHabitsResult(retenues, proposees.EstRepli);
    }
}
