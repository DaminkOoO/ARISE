using Arise.Application.Features.Sport.Commands.GenerateTodayQuest;
using FluentAssertions;
using FluentValidation.Results;

namespace Arise.Application.Tests.Features.Sport.Commands;

/// <summary>
/// La commande de génération est envoyée par la requête de lecture aujourd'hui, par le
/// <c>briefing-worker</c> demain : ses contrôles de forme ne peuvent pas s'appuyer sur ceux de
/// l'appelant.
/// </summary>
public class GenerateTodayQuestCommandValidatorTests
{
    private static readonly Guid ProfilValide = Guid.NewGuid();

    private static readonly GenerateTodayQuestCommandValidator Validator = new();

    private static ValidationResult Valide(Guid? hunterProfileId = null, DateOnly? jour = null) =>
        Validator.Validate(new GenerateTodayQuestCommand(
            hunterProfileId ?? ProfilValide, jour ?? new DateOnly(2026, 7, 26)));

    private static string PremiereErreurSur(ValidationResult resultat, string propriete) =>
        resultat.Errors.Should().Contain(erreur => erreur.PropertyName == propriete)
            .Which.ErrorMessage;

    [Fact]
    public void Accepte_une_commande_bien_remplie()
    {
        Valide().IsValid.Should().BeTrue();
    }

    [Fact]
    public void Refuse_un_identifiant_de_profil_vide()
    {
        var resultat = Valide(hunterProfileId: Guid.Empty);

        PremiereErreurSur(resultat, nameof(GenerateTodayQuestCommand.HunterProfileId))
            .Should().Be("Le profil de Chasseur ciblé est obligatoire.");
    }

    // Une DateOnly par défaut vaut le 1er janvier de l'an 1 : elle passerait tous les contrôles
    // de type et poserait une quête que personne ne relira jamais.
    [Fact]
    public void Refuse_une_date_de_quete_absente()
    {
        var resultat = Valide(jour: default(DateOnly));

        PremiereErreurSur(resultat, nameof(GenerateTodayQuestCommand.QuestDate))
            .Should().Be("Le jour de la quête est obligatoire.");
    }
}
