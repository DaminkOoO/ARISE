using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Domain.Habits;
using MediatR;

namespace Arise.Application.Features.Habits.Commands.CreateHabit;

/// <summary>
/// Déclare une habitude et rien de plus : aucun XP accordé, aucune série touchée. Se donner une
/// intention ne fait pas progresser le Chasseur — c'est <c>LogHabitCommand</c> qui le fera, à
/// chaque fois qu'il l'aura tenue.
///
/// <para>Le contrôle d'unicité qui suit est un confort d'affichage, pas une garantie : entre la
/// lecture et l'écriture, une seconde déclaration du même nom peut se glisser. Ce qui tranchera
/// réellement est l'index unique posé avec la table — ici, on se contente d'expliquer le conflit
/// en français plutôt que de laisser remonter une violation de contrainte.</para>
/// </summary>
public sealed class CreateHabitCommandHandler(
    IHunterProfileRepository hunterProfiles,
    IHabitRepository habits,
    TimeProvider timeProvider)
    : IRequestHandler<CreateHabitCommand, CreateHabitResult>
{
    public async Task<CreateHabitResult> Handle(
        CreateHabitCommand request, CancellationToken cancellationToken)
    {
        // La clé étrangère refuserait de toute façon l'écriture, mais loin d'ici et dans une
        // langue que le Chasseur n'a pas à lire.
        var profil = await hunterProfiles.GetByIdAsync(request.HunterProfileId, cancellationToken)
            ?? throw new HunterProfileNotFoundException();

        // Le nom rogné est celui que portera l'habitude : chercher le brut laisserait passer
        // « Courir » puis « Courir  » comme deux déclarations distinctes.
        var nom = request.Name.Trim();

        if (await habits.ExistsWithNameAsync(profil.Id, nom, cancellationToken))
        {
            throw new HabitNameAlreadyTakenException();
        }

        var habitude = Habit.Create(
            profil.Id, nom, request.Frequency, timeProvider.GetUtcNow());

        await habits.AddAsync(habitude, cancellationToken);

        return new CreateHabitResult(habitude.Id, habitude.Name, habitude.Frequency);
    }
}
