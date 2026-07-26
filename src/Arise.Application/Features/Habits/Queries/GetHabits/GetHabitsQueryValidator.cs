using FluentValidation;

namespace Arise.Application.Features.Habits.Queries.GetHabits;

/// <summary>
/// Contrôle de forme de la demande de liste. Le message est écrit explicitement : il remonte
/// jusqu'à l'écran (règle n°7), en français.
/// </summary>
public sealed class GetHabitsQueryValidator : AbstractValidator<GetHabitsQuery>
{
    public GetHabitsQueryValidator()
    {
        // Sans ce contrôle, un identifiant vide interrogerait la base pour rien et rendrait une
        // liste vide indiscernable de celle d'un Chasseur qui n'a encore rien déclaré.
        RuleFor(requete => requete.HunterProfileId)
            .NotEmpty()
                .WithMessage("Le profil de Chasseur ciblé est obligatoire.");
    }
}
