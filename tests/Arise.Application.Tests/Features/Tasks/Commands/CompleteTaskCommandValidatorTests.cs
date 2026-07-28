using Arise.Application.Features.Tasks.Commands.CompleteTask;
using FluentAssertions;
using FluentValidation.Results;

namespace Arise.Application.Tests.Features.Tasks.Commands;

/// <summary>
/// Contrôles de forme de la complétion d'une tâche. Le fuseau y est décisif non pour dater la
/// complétion — un instant absolu suffirait — mais parce que le plafond quotidien d'XP
/// d'engagement se recompte sur la journée du Chasseur.
/// </summary>
public class CompleteTaskCommandValidatorTests
{
    private static readonly Guid ProfilValide = Guid.NewGuid();

    private static readonly Guid TacheValide = Guid.NewGuid();

    private static readonly CompleteTaskCommandValidator Validator = new();

    private static ValidationResult Valide(
        Guid? hunterProfileId = null, Guid? taskId = null, string fuseau = "Europe/Paris") =>
        Validator.Validate(new CompleteTaskCommand(
            hunterProfileId ?? ProfilValide, taskId ?? TacheValide, fuseau));

    private static string PremiereErreurSur(ValidationResult resultat, string propriete) =>
        resultat.Errors.Should().Contain(erreur => erreur.PropertyName == propriete)
            .Which.ErrorMessage;

    [Fact]
    public void Accepte_une_completion_bien_remplie()
    {
        Valide().IsValid.Should().BeTrue();
    }

    [Fact]
    public void Refuse_un_identifiant_de_profil_vide()
    {
        PremiereErreurSur(Valide(hunterProfileId: Guid.Empty), nameof(CompleteTaskCommand.HunterProfileId))
            .Should().Be("Le profil de Chasseur ciblé est obligatoire.");
    }

    [Fact]
    public void Refuse_un_identifiant_de_tache_vide()
    {
        PremiereErreurSur(Valide(taskId: Guid.Empty), nameof(CompleteTaskCommand.TaskId))
            .Should().Be("La tâche à cocher est obligatoire.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Refuse_un_fuseau_horaire_absent(string fuseau)
    {
        PremiereErreurSur(Valide(fuseau: fuseau), nameof(CompleteTaskCommand.FuseauHoraire))
            .Should().Be("Le fuseau horaire du Chasseur est obligatoire.");
    }

    // Sans ce contrôle, un fuseau inconnu ferait lever le handler avec une
    // TimeZoneNotFoundException — une erreur technique, en anglais, au moment de recompter le
    // plafond.
    [Fact]
    public void Refuse_un_fuseau_horaire_inconnu()
    {
        PremiereErreurSur(
                Valide(fuseau: "Terre/Portail-de-Jeju"),
                nameof(CompleteTaskCommand.FuseauHoraire))
            .Should().Be("Ce fuseau horaire est inconnu.");
    }
}
