using Arise.Application.Features.Sport.Queries.GetTodayGymQuest;
using FluentAssertions;
using FluentValidation.Results;

namespace Arise.Application.Tests.Features.Sport.Queries;

/// <summary>
/// Le fuseau est le seul champ dont la validité conditionne un calcul de date : un
/// identifiant inconnu ferait lever le handler loin du site fautif, avec une erreur technique
/// que le Chasseur n'a pas à lire.
/// </summary>
public class GetTodayGymQuestQueryValidatorTests
{
    private static readonly Guid ProfilValide = Guid.NewGuid();

    private static readonly GetTodayGymQuestQueryValidator Validator = new();

    private static ValidationResult Valide(
        Guid? hunterProfileId = null, string fuseau = "Europe/Paris") =>
        Validator.Validate(new GetTodayGymQuestQuery(hunterProfileId ?? ProfilValide, fuseau));

    private static string PremiereErreurSur(ValidationResult resultat, string propriete) =>
        resultat.Errors.Should().Contain(erreur => erreur.PropertyName == propriete)
            .Which.ErrorMessage;

    [Fact]
    public void Accepte_une_demande_bien_remplie()
    {
        Valide().IsValid.Should().BeTrue();
    }

    [Fact]
    public void Refuse_un_identifiant_de_profil_vide()
    {
        var resultat = Valide(hunterProfileId: Guid.Empty);

        PremiereErreurSur(resultat, nameof(GetTodayGymQuestQuery.HunterProfileId))
            .Should().Be("Le profil de Chasseur ciblé est obligatoire.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Refuse_un_fuseau_horaire_absent(string fuseau)
    {
        var resultat = Valide(fuseau: fuseau);

        PremiereErreurSur(resultat, nameof(GetTodayGymQuestQuery.FuseauHoraire))
            .Should().Be("Le fuseau horaire du Chasseur est obligatoire.");
    }

    [Fact]
    public void Refuse_un_fuseau_horaire_inconnu()
    {
        var resultat = Valide(fuseau: "Terre/Portail-de-Jeju");

        PremiereErreurSur(resultat, nameof(GetTodayGymQuestQuery.FuseauHoraire))
            .Should().Be("Ce fuseau horaire est inconnu.");
    }

    // UTC est un identifiant valide partout, et c'est celui qu'un client sans fuseau connu
    // enverra : le refuser rendrait la quête du jour inatteignable pour lui.
    [Fact]
    public void Accepte_le_fuseau_UTC()
    {
        Valide(fuseau: "UTC").IsValid.Should().BeTrue();
    }
}
