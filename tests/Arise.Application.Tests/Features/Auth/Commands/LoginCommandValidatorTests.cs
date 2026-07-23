using Arise.Application.Features.Auth.Commands.Login;
using FluentAssertions;
using FluentValidation.Results;

namespace Arise.Application.Tests.Features.Auth.Commands;

public class LoginCommandValidatorTests
{
    private const string NomValide = "sung-jin-woo";
    private const string MotDePasseValide = "Éveil-du-Système-2026";

    private static readonly LoginCommandValidator Validator = new();

    private static ValidationResult Valide(
        string nomUtilisateur = NomValide,
        string motDePasse = MotDePasseValide) =>
        Validator.Validate(new LoginCommand(nomUtilisateur, motDePasse));

    private static string PremiereErreurSur(ValidationResult resultat, string propriete) =>
        resultat.Errors.Should().Contain(erreur => erreur.PropertyName == propriete)
            .Which.ErrorMessage;

    [Fact]
    public void Accepte_une_connexion_bien_remplie()
    {
        Valide().IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Refuse_un_nom_de_Chasseur_vide(string nomUtilisateur)
    {
        var resultat = Valide(nomUtilisateur: nomUtilisateur);

        PremiereErreurSur(resultat, nameof(LoginCommand.Username))
            .Should().Be("Le nom de Chasseur est obligatoire.");
    }

    [Fact]
    public void Refuse_un_mot_de_passe_vide()
    {
        var resultat = Valide(motDePasse: "");

        PremiereErreurSur(resultat, nameof(LoginCommand.Password))
            .Should().Be("Le mot de passe est obligatoire.");
    }

    // Le handler rogne avant de chercher le compte : refuser ce nom ici couperait la
    // connexion à qui a effleuré la barre d'espace après son nom.
    [Fact]
    public void Accepte_un_nom_entoure_d_espaces()
    {
        Valide(nomUtilisateur: "  " + NomValide + "  ").IsValid.Should().BeTrue();
    }

    // La connexion ne rejoue pas les règles de l'inscription : elles ne protègent rien ici
    // — le mot de passe est déjà choisi — et refuser la tentative avant de l'avoir vérifiée
    // enfermerait dehors tout compte créé sous une politique antérieure.
    [Theory]
    [InlineData("court")]
    [InlineData("a")]
    public void N_impose_aucune_longueur_minimale_au_mot_de_passe(string motDePasse)
    {
        Valide(motDePasse: motDePasse).IsValid.Should().BeTrue();
    }

    // Le handler rogne le nom avant de chercher le compte : le même vecteur qu'à
    // l'inscription, sur une route tout aussi ouverte.
    [Fact]
    public void Refuse_un_nom_de_Chasseur_noye_dans_les_espaces()
    {
        Valide(nomUtilisateur: new string(' ', 100_000) + NomValide)
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void N_impose_aucun_format_au_nom_de_Chasseur()
    {
        Valide(nomUtilisateur: "nom@invalide").IsValid.Should().BeTrue();
    }

    // Vérifier une empreinte coûte autant que la calculer : la route de connexion est
    // ouverte sans authentification, le plafond y est donc aussi nécessaire qu'à
    // l'inscription.
    [Fact]
    public void Refuse_un_mot_de_passe_demesure()
    {
        var resultat = Valide(motDePasse: new string('a', 129));

        PremiereErreurSur(resultat, nameof(LoginCommand.Password))
            .Should().Be("Le mot de passe ne peut pas dépasser 128 caractères.");
    }
}
