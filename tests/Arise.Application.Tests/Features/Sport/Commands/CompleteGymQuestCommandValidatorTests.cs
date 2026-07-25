using Arise.Application.Features.Sport.Commands.CompleteGymQuest;
using FluentAssertions;
using FluentValidation.Results;

namespace Arise.Application.Tests.Features.Sport.Commands;

/// <summary>
/// Contrôles de forme de la complétion. Le fuseau y est aussi décisif que dans la demande de
/// quête du jour : c'est lui qui décide du jour que la série comptera.
/// </summary>
public class CompleteGymQuestCommandValidatorTests
{
    private static readonly Guid ProfilValide = Guid.NewGuid();

    private static readonly Guid QueteValide = Guid.NewGuid();

    private static readonly CompleteGymQuestCommandValidator Validator = new();

    private static ValidationResult Valide(
        Guid? hunterProfileId = null, Guid? questId = null, string fuseau = "Europe/Paris") =>
        Validator.Validate(new CompleteGymQuestCommand(
            hunterProfileId ?? ProfilValide, questId ?? QueteValide, fuseau));

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
        var resultat = Valide(hunterProfileId: Guid.Empty);

        PremiereErreurSur(resultat, nameof(CompleteGymQuestCommand.HunterProfileId))
            .Should().Be("Le profil de Chasseur ciblé est obligatoire.");
    }

    [Fact]
    public void Refuse_un_identifiant_de_quete_vide()
    {
        var resultat = Valide(questId: Guid.Empty);

        PremiereErreurSur(resultat, nameof(CompleteGymQuestCommand.QuestId))
            .Should().Be("La quête à accomplir est obligatoire.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Refuse_un_fuseau_horaire_absent(string fuseau)
    {
        var resultat = Valide(fuseau: fuseau);

        PremiereErreurSur(resultat, nameof(CompleteGymQuestCommand.FuseauHoraire))
            .Should().Be("Le fuseau horaire du Chasseur est obligatoire.");
    }

    [Fact]
    public void Refuse_un_fuseau_horaire_inconnu()
    {
        var resultat = Valide(fuseau: "Terre/Portail-de-Jeju");

        PremiereErreurSur(resultat, nameof(CompleteGymQuestCommand.FuseauHoraire))
            .Should().Be("Ce fuseau horaire est inconnu.");
    }

    [Fact]
    public void Accepte_le_fuseau_UTC()
    {
        Valide(fuseau: "UTC").IsValid.Should().BeTrue();
    }
}
