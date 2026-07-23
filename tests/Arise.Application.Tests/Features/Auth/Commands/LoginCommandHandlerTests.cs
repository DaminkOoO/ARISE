using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Application.Features.Auth.Commands.Login;
using Arise.Domain.Users;
using FluentAssertions;
using NSubstitute;

namespace Arise.Application.Tests.Features.Auth.Commands;

public class LoginCommandHandlerTests
{
    private const string NomUtilisateur = "sung-jin-woo";
    private const string MotDePasse = "Éveil-du-Système-2026";
    private const string Empreinte = "empreinte-de-Éveil-du-Système-2026";

    private static readonly DateTimeOffset Expiration =
        new(2026, 7, 23, 10, 0, 0, TimeSpan.Zero);

    private static readonly JwtToken Jeton = new("jeton-jwt-signé", Expiration);

    private static readonly User Chasseur = User.Register(
        NomUtilisateur, Empreinte, new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero));

    private readonly IUserRepository _chasseurs = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hacheur = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenGenerator _jetons = Substitute.For<IJwtTokenGenerator>();

    public LoginCommandHandlerTests()
    {
        // Par défaut, le Chasseur existe et son mot de passe est le bon : chaque test qui
        // teste un échec le dit lui-même.
        _chasseurs.FindByUsernameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Chasseur);
        _hacheur.Verify(MotDePasse, Empreinte).Returns(true);
        _jetons.Generate(Arg.Any<User>()).Returns(Jeton);
    }

    private Task<LoginResult> Connecter(
        string nomUtilisateur = NomUtilisateur,
        string motDePasse = MotDePasse) =>
        new LoginCommandHandler(_chasseurs, _hacheur, _jetons).Handle(
            new LoginCommand(nomUtilisateur, motDePasse),
            CancellationToken.None);

    [Fact]
    public async Task Renvoie_le_jeton_emis_pour_le_Chasseur()
    {
        (await Connecter()).AccessToken.Should().Be(Jeton.Value);
    }

    [Fact]
    public async Task Renvoie_l_expiration_du_jeton()
    {
        (await Connecter()).ExpiresAt.Should().Be(Expiration);
    }

    [Fact]
    public async Task Emet_le_jeton_pour_le_Chasseur_trouve()
    {
        await Connecter();

        _jetons.Received(1).Generate(Chasseur);
    }

    [Fact]
    public async Task Verifie_le_mot_de_passe_contre_l_empreinte_stockee()
    {
        await Connecter();

        _hacheur.Received(1).Verify(MotDePasse, Empreinte);
    }

    [Fact]
    public async Task Refuse_un_mot_de_passe_incorrect()
    {
        _hacheur.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var acte = () => Connecter(motDePasse: "mauvais-mot-de-passe");

        await acte.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task N_emet_aucun_jeton_quand_le_mot_de_passe_est_incorrect()
    {
        _hacheur.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var acte = () => Connecter(motDePasse: "mauvais-mot-de-passe");

        await acte.Should().ThrowAsync<InvalidCredentialsException>();
        _jetons.DidNotReceive().Generate(Arg.Any<User>());
    }

    [Fact]
    public async Task Refuse_un_Chasseur_inconnu()
    {
        _chasseurs.FindByUsernameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var acte = () => Connecter(nomUtilisateur: "chasseur-inexistant");

        await acte.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task N_emet_aucun_jeton_pour_un_Chasseur_inconnu()
    {
        _chasseurs.FindByUsernameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var acte = () => Connecter(nomUtilisateur: "chasseur-inexistant");

        await acte.Should().ThrowAsync<InvalidCredentialsException>();
        _jetons.DidNotReceive().Generate(Arg.Any<User>());
    }

    // Deux messages distincts diraient à qui essaie des noms au hasard lesquels existent.
    [Fact]
    public async Task Dit_la_meme_chose_sur_un_nom_inconnu_et_sur_un_mot_de_passe_faux()
    {
        _hacheur.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var surMotDePasseFaux = (await ((Func<Task>)(() => Connecter()))
            .Should().ThrowAsync<InvalidCredentialsException>()).Which.Message;

        _chasseurs.FindByUsernameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);
        var surNomInconnu = (await ((Func<Task>)(() => Connecter()))
            .Should().ThrowAsync<InvalidCredentialsException>()).Which.Message;

        surNomInconnu.Should().Be(surMotDePasseFaux);
    }

    // Le message remonte jusqu'à l'écran : règle non négociable n°7.
    [Fact]
    public async Task Explique_l_echec_en_francais()
    {
        _hacheur.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var acte = () => Connecter();

        (await acte.Should().ThrowAsync<InvalidCredentialsException>())
            .Which.Message.Should().Be(
                "Nom de Chasseur ou mot de passe incorrect.");
    }

    // Le compte porte un nom rogné : chercher le brut refuserait la connexion à qui a
    // effleuré la barre d'espace après son nom.
    [Fact]
    public async Task Cherche_le_Chasseur_sur_le_nom_rogne()
    {
        await Connecter(nomUtilisateur: "  sung-jin-woo  ");

        await _chasseurs.Received(1)
            .FindByUsernameAsync(NomUtilisateur, Arg.Any<CancellationToken>());
    }

    // Le mot de passe part au hacheur tel qu'il a été saisi : le rogner amputerait une
    // phrase de passe qui commence ou finit par une espace délibérée.
    [Fact]
    public async Task Ne_rogne_pas_le_mot_de_passe_soumis()
    {
        const string avecEspaces = "  " + MotDePasse + "  ";
        _hacheur.Verify(avecEspaces, Empreinte).Returns(true);

        await Connecter(motDePasse: avecEspaces);

        _hacheur.Received(1).Verify(avecEspaces, Empreinte);
    }
}
