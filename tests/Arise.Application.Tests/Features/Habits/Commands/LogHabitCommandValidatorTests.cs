using Arise.Application.Features.Habits.Commands.LogHabit;
using FluentAssertions;
using FluentValidation.Results;

namespace Arise.Application.Tests.Features.Habits.Commands;

/// <summary>
/// Contrôles de forme de la journalisation. Le fuseau y est aussi décisif que dans la complétion
/// d'une quête : c'est lui qui décide du jour auquel l'effort sera daté, donc de ce que la série
/// de l'habitude comptera.
/// </summary>
public class LogHabitCommandValidatorTests
{
    private static readonly Guid ProfilValide = Guid.NewGuid();

    private static readonly Guid HabitudeValide = Guid.NewGuid();

    private static readonly LogHabitCommandValidator Validator = new();

    private static ValidationResult Valide(
        Guid? hunterProfileId = null, Guid? habitId = null, string fuseau = "Europe/Paris") =>
        Validator.Validate(new LogHabitCommand(
            hunterProfileId ?? ProfilValide, habitId ?? HabitudeValide, fuseau));

    private static string PremiereErreurSur(ValidationResult resultat, string propriete) =>
        resultat.Errors.Should().Contain(erreur => erreur.PropertyName == propriete)
            .Which.ErrorMessage;

    [Fact]
    public void Accepte_une_journalisation_bien_remplie()
    {
        Valide().IsValid.Should().BeTrue();
    }

    [Fact]
    public void Refuse_un_identifiant_de_profil_vide()
    {
        var resultat = Valide(hunterProfileId: Guid.Empty);

        PremiereErreurSur(resultat, nameof(LogHabitCommand.HunterProfileId))
            .Should().Be("Le profil de Chasseur ciblé est obligatoire.");
    }

    [Fact]
    public void Refuse_un_identifiant_d_habitude_vide()
    {
        var resultat = Valide(habitId: Guid.Empty);

        PremiereErreurSur(resultat, nameof(LogHabitCommand.HabitId))
            .Should().Be("L'habitude à journaliser est obligatoire.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Refuse_un_fuseau_horaire_absent(string fuseau)
    {
        var resultat = Valide(fuseau: fuseau);

        PremiereErreurSur(resultat, nameof(LogHabitCommand.FuseauHoraire))
            .Should().Be("Le fuseau horaire du Chasseur est obligatoire.");
    }

    [Fact]
    public void Refuse_un_fuseau_horaire_inconnu()
    {
        var resultat = Valide(fuseau: "Terre/Portail-de-Jeju");

        PremiereErreurSur(resultat, nameof(LogHabitCommand.FuseauHoraire))
            .Should().Be("Ce fuseau horaire est inconnu.");
    }

    [Fact]
    public void Accepte_le_fuseau_UTC()
    {
        Valide(fuseau: "UTC").IsValid.Should().BeTrue();
    }
}
