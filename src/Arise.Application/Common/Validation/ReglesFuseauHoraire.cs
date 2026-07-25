using FluentValidation;

namespace Arise.Application.Common.Validation;

/// <summary>
/// Le contrôle du fuseau horaire du Chasseur, à un seul endroit. Deux requêtes le portent déjà
/// — la quête du jour et sa complétion — et chacune des quatre phases de domaine en ajoutera :
/// recopier la règle, c'est laisser les deux messages français diverger au premier
/// réajustement de ton.
/// </summary>
public static class ReglesFuseauHoraire
{
    public static IRuleBuilderOptions<T, string> FuseauHoraireDuChasseur<T>(
        this IRuleBuilder<T, string> regle) =>
        regle
            .NotEmpty()
                .WithMessage("Le fuseau horaire du Chasseur est obligatoire.")
            .Must(EstUnFuseauConnu)
                .WithMessage("Ce fuseau horaire est inconnu.");

    /// <summary>
    /// <c>TryFindSystemTimeZoneById</c> et non <c>FindSystemTimeZoneById</c> : un fuseau inconnu
    /// est ici une saisie à refuser poliment, pas une exception à lever.
    /// </summary>
    private static bool EstUnFuseauConnu(string fuseauHoraire) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(fuseauHoraire, out _);
}
