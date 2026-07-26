using Arise.Application.Features.Habits.Queries.GetHabits;
using FluentAssertions;
using FluentValidation.Results;

namespace Arise.Application.Tests.Features.Habits.Queries;

/// <summary>
/// Le seul champ de la requête est le Chasseur visé. Sans ce contrôle, un identifiant vide
/// interrogerait la base pour rien et rendrait une liste vide indiscernable de celle d'un
/// Chasseur qui n'a encore rien déclaré.
/// </summary>
public class GetHabitsQueryValidatorTests
{
    private static readonly GetHabitsQueryValidator Validator = new();

    private static ValidationResult Valide(Guid? hunterProfileId = null) =>
        Validator.Validate(new GetHabitsQuery(hunterProfileId ?? Guid.NewGuid()));

    [Fact]
    public void Accepte_une_demande_bien_remplie()
    {
        Valide().IsValid.Should().BeTrue();
    }

    [Fact]
    public void Refuse_un_identifiant_de_profil_vide()
    {
        var resultat = Valide(hunterProfileId: Guid.Empty);

        resultat.Errors.Should()
            .Contain(erreur => erreur.PropertyName == nameof(GetHabitsQuery.HunterProfileId))
            .Which.ErrorMessage.Should().Be("Le profil de Chasseur ciblé est obligatoire.");
    }
}
