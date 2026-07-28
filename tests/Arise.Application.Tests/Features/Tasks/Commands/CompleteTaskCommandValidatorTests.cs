using Arise.Application.Features.Tasks.Commands.CompleteTask;
using FluentAssertions;
using FluentValidation.Results;

namespace Arise.Application.Tests.Features.Tasks.Commands;

/// <summary>
/// Contrôles de forme de la complétion d'une tâche. Pas de fuseau horaire à valider,
/// contrairement à la complétion d'une quête : rien ici ne dépend du jour du Chasseur.
/// </summary>
public class CompleteTaskCommandValidatorTests
{
    private static readonly Guid ProfilValide = Guid.NewGuid();

    private static readonly Guid TacheValide = Guid.NewGuid();

    private static readonly CompleteTaskCommandValidator Validator = new();

    private static ValidationResult Valide(Guid? hunterProfileId = null, Guid? taskId = null) =>
        Validator.Validate(new CompleteTaskCommand(
            hunterProfileId ?? ProfilValide, taskId ?? TacheValide));

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
}
