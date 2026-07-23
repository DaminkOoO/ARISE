using Arise.Application.Features.Auth.Commands.RegisterUser;
using FluentAssertions;
using FluentValidation.Results;

namespace Arise.Application.Tests.Features.Auth.Commands;

public class RegisterUserCommandValidatorTests
{
    private const string NomValide = "sung-jin-woo";
    private const string MotDePasseValide = "Éveil-du-Système-2026";

    private static readonly RegisterUserCommandValidator Validator = new();

    private static ValidationResult Valide(
        string nomUtilisateur = NomValide,
        string motDePasse = MotDePasseValide) =>
        Validator.Validate(new RegisterUserCommand(nomUtilisateur, motDePasse));

    private static string PremiereErreurSur(ValidationResult resultat, string propriete) =>
        resultat.Errors.Should().Contain(erreur => erreur.PropertyName == propriete)
            .Which.ErrorMessage;

    [Fact]
    public void Accepte_une_inscription_bien_remplie()
    {
        Valide().IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Refuse_un_nom_de_Chasseur_vide(string nomUtilisateur)
    {
        var resultat = Valide(nomUtilisateur: nomUtilisateur);

        PremiereErreurSur(resultat, nameof(RegisterUserCommand.Username))
            .Should().Be("Le nom de Chasseur est obligatoire.");
    }

    // Le handler rogne avant d'enregistrer : valider le brut accepterait « ab  » pour un
    // compte qui s'appellera « ab », trop court.
    [Fact]
    public void Mesure_la_longueur_du_nom_une_fois_rogne()
    {
        Valide(nomUtilisateur: "  ab  ").IsValid.Should().BeFalse();
    }

    // Le miroir du test précédent : puisque le handler rognera, refuser ce nom pour ses
    // espaces de bordure rejetterait une inscription que le handler aurait acceptée.
    [Fact]
    public void Accepte_un_nom_valide_entoure_d_espaces()
    {
        Valide(nomUtilisateur: "  " + NomValide + "  ").IsValid.Should().BeTrue();
    }

    [Fact]
    public void Refuse_un_nom_de_Chasseur_trop_court()
    {
        var resultat = Valide(nomUtilisateur: "ab");

        PremiereErreurSur(resultat, nameof(RegisterUserCommand.Username))
            .Should().Be("Le nom de Chasseur doit contenir au moins 3 caractères.");
    }

    [Fact]
    public void Refuse_un_nom_de_Chasseur_trop_long()
    {
        var resultat = Valide(nomUtilisateur: new string('a', 33));

        PremiereErreurSur(resultat, nameof(RegisterUserCommand.Username))
            .Should().Be("Le nom de Chasseur ne peut pas dépasser 32 caractères.");
    }

    [Theory]
    [InlineData("sung jin woo")] // L'espace intérieur rend deux noms indiscernables à l'œil.
    [InlineData("sung@arise")]
    [InlineData("sung/../woo")]
    public void Refuse_un_nom_de_Chasseur_aux_caracteres_interdits(string nomUtilisateur)
    {
        var resultat = Valide(nomUtilisateur: nomUtilisateur);

        PremiereErreurSur(resultat, nameof(RegisterUserCommand.Username))
            .Should().Be(
                "Le nom de Chasseur ne peut contenir que des lettres, des chiffres, "
                + "des tirets et des tirets bas.");
    }

    [Theory]
    [InlineData("sung-jin-woo")]
    [InlineData("sung_jin_woo")]
    [InlineData("Chasseur42")]
    [InlineData("Séraphin")] // L'app est française : les accents ne sont pas des intrus.
    public void Accepte_les_noms_de_Chasseur_legitimes(string nomUtilisateur)
    {
        Valide(nomUtilisateur: nomUtilisateur).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Refuse_un_mot_de_passe_vide()
    {
        var resultat = Valide(motDePasse: "");

        PremiereErreurSur(resultat, nameof(RegisterUserCommand.Password))
            .Should().Be("Le mot de passe est obligatoire.");
    }

    [Fact]
    public void Refuse_un_mot_de_passe_trop_court()
    {
        var resultat = Valide(motDePasse: "1234567");

        PremiereErreurSur(resultat, nameof(RegisterUserCommand.Password))
            .Should().Be("Le mot de passe doit contenir au moins 8 caractères.");
    }

    [Fact]
    public void Accepte_un_mot_de_passe_de_huit_caracteres()
    {
        Valide(motDePasse: "12345678").IsValid.Should().BeTrue();
    }

    // Hacher est coûteux par construction : sans plafond, une requête non authentifiée
    // portant un mot de passe d'un mégaoctet fait payer le serveur.
    [Fact]
    public void Refuse_un_mot_de_passe_demesure()
    {
        var resultat = Valide(motDePasse: new string('a', 129));

        PremiereErreurSur(resultat, nameof(RegisterUserCommand.Password))
            .Should().Be("Le mot de passe ne peut pas dépasser 128 caractères.");
    }

    // Les espaces peuvent être délibérés dans une phrase de passe : les rogner amputerait
    // silencieusement le secret, et la connexion échouerait sans explication.
    [Fact]
    public void Ne_rogne_pas_le_mot_de_passe()
    {
        Valide(motDePasse: "  " + MotDePasseValide + "  ").IsValid.Should().BeTrue();
    }
}
