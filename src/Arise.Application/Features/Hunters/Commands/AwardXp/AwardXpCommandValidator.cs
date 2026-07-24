using FluentValidation;

namespace Arise.Application.Features.Hunters.Commands.AwardXp;

/// <summary>
/// Contrôles de forme de l'attribution d'XP. La règle métier (l'XP ne se retire jamais) vit
/// dans <see cref="Arise.Domain.Hunters.HunterProfile.AwardXp"/> ; ce validator ne fait que
/// refuser en amont, avec un message en français, ce que le domaine refuserait de toute
/// façon par une exception technique.
/// </summary>
public sealed class AwardXpCommandValidator : AbstractValidator<AwardXpCommand>
{
    public AwardXpCommandValidator()
    {
        RuleFor(commande => commande.HunterProfileId)
            .NotEmpty()
                .WithMessage("Le profil de Chasseur ciblé est obligatoire.");

        RuleFor(commande => commande.Montant)
            .GreaterThan(0)
                .WithMessage("Le montant d'XP doit être strictement positif.");
    }
}
