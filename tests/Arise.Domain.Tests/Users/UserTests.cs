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

    // La borne haute est une invariante de l'entité, pas seulement une règle de formulaire :
    // la colonne est un character varying(32). Tout chemin d'écriture qui ne passe pas par
    // RegisterUserCommand — seed, import, onboarding généré par un agent — construirait
    // sinon un User que le Domain accepte et que PostgreSQL refuse au SaveChangesAsync,
    // loin du site fautif.
    [Fact]
    public void Register_refuse_un_nom_d_utilisateur_trop_long()
    {
        var acte = () => Inscrire(nomUtilisateur: new string('a', 33));

        acte.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Register_accepte_un_nom_d_utilisateur_a_la_borne_haute()
    {
        Inscrire(nomUtilisateur: new string('a', 32)).Username.Should().HaveLength(32);
    }

    [Fact]
    public void Register_refuse_un_nom_d_utilisateur_trop_court()
    {
        var acte = () => Inscrire(nomUtilisateur: "ab");

        acte.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Register_accepte_un_nom_d_utilisateur_a_la_borne_basse()
    {
        Inscrire(nomUtilisateur: "abc").Username.Should().Be("abc");
    }

    // Les bornes portent sur le nom rogné, qui est celui du compte.
    [Fact]
    public void Register_mesure_les_bornes_du_nom_une_fois_rogne()
    {
        Inscrire(nomUtilisateur: "  " + new string('a', 32) + "  ")
            .Username.Should().HaveLength(32);
    }

    // Npgsql refuse d'écrire un DateTimeOffset d'offset non nul dans un
    // timestamp with time zone. Sans cette garde, la panne sort au SaveChangesAsync, loin de
    // l'appelant qui a passé un DateTimeOffset.Now.
    [Fact]
    public void Register_refuse_un_instant_d_inscription_d_offset_non_nul()
    {
        var acte = () => User.Register(
            NomUtilisateur, Empreinte, new DateTimeOffset(2026, 7, 23, 11, 0, 0, TimeSpan.FromHours(2)));

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

    // --- Rattachement du profil de progression ------------------------------------------------

    // Un compte existe dès l'inscription et vit sans profil jusqu'à l'éveil : la relation est
    // facultative de ce côté-là, et c'est pourquoi la clé est portée ici.
    [Fact]
    public void Un_compte_fraichement_inscrit_ne_porte_aucun_profil()
    {
        Inscrire().HunterProfileId.Should().BeNull();
    }

    [Fact]
    public void Rattache_le_profil_eveille_au_compte()
    {
        var compte = Inscrire();
        var profil = Guid.NewGuid();

        compte.RattacherLeProfil(profil);

        compte.HunterProfileId.Should().Be(profil);
    }

    // Un renvoi réseau de l'onboarding ne doit pas échouer sur un geste qui ne change rien.
    [Fact]
    public void Rattacher_deux_fois_le_meme_profil_ne_leve_pas()
    {
        var compte = Inscrire();
        var profil = Guid.NewGuid();

        compte.RattacherLeProfil(profil);
        var acte = () => compte.RattacherLeProfil(profil);

        acte.Should().NotThrow();
    }

    // Le laisser passer écraserait silencieusement toute la progression du Chasseur : son
    // ancien profil deviendrait inatteignable, XP et séries compris.
    [Fact]
    public void Refuse_de_rattacher_un_second_profil_different()
    {
        var compte = Inscrire();
        compte.RattacherLeProfil(Guid.NewGuid());

        var acte = () => compte.RattacherLeProfil(Guid.NewGuid());

        acte.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Refuse_de_rattacher_un_profil_sans_identifiant()
    {
        var acte = () => Inscrire().RattacherLeProfil(Guid.Empty);

        acte.Should().Throw<ArgumentException>();
    }
}
