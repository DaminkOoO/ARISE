using Arise.Domain.Users;
using FluentAssertions;

namespace Arise.Domain.Tests.Users;

public class UserTests
{
    private const string NomUtilisateur = "sung-jin-woo";
    private const string Empreinte = "AQAAAAIAAYagAAAAE…empreinte-calculée-ailleurs";

    private static readonly DateTimeOffset Inscription =
        new(2026, 7, 23, 9, 0, 0, TimeSpan.Zero);

    private static User Inscrire(
        string nomUtilisateur = NomUtilisateur,
        string empreinte = Empreinte) =>
        User.Register(nomUtilisateur, empreinte, Inscription);

    [Fact]
    public void Register_retient_le_nom_d_utilisateur()
    {
        Inscrire().Username.Should().Be(NomUtilisateur);
    }

    [Fact]
    public void Register_retient_l_empreinte_du_mot_de_passe()
    {
        Inscrire().PasswordHash.Should().Be(Empreinte);
    }

    [Fact]
    public void Register_retient_l_instant_d_inscription()
    {
        Inscrire().RegisteredAt.Should().Be(Inscription);
    }

    [Fact]
    public void Register_attribue_un_identifiant()
    {
        Inscrire().Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Register_attribue_un_identifiant_distinct_a_chaque_Chasseur()
    {
        var premier = Inscrire();
        var second = Inscrire();

        second.Id.Should().NotBe(premier.Id);
    }

    // Les espaces de bordure sont invisibles à la saisie : sans rognage, « sung » et
    // « sung  » deviennent deux Chasseurs distincts, et le second ne peut plus se connecter
    // avec ce qu'il croit être son nom.
    [Fact]
    public void Register_rogne_les_espaces_autour_du_nom_d_utilisateur()
    {
        Inscrire(nomUtilisateur: "  sung-jin-woo  ").Username.Should().Be(NomUtilisateur);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_refuse_un_nom_d_utilisateur_vide(string nomUtilisateur)
    {
        var acte = () => Inscrire(nomUtilisateur: nomUtilisateur);

        acte.Should().Throw<ArgumentException>();
    }

    // Une empreinte vide passerait le stockage sans bruit, et toute vérification ultérieure
    // se ferait alors contre rien.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_refuse_une_empreinte_vide(string empreinte)
    {
        var acte = () => Inscrire(empreinte: empreinte);

        acte.Should().Throw<ArgumentException>();
    }
}
