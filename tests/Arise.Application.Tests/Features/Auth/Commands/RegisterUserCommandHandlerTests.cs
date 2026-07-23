using Arise.Application.Common.Abstractions;
using Arise.Application.Common.Exceptions;
using Arise.Application.Features.Auth.Commands.RegisterUser;
using Arise.Domain.Users;
using FluentAssertions;
using NSubstitute;

namespace Arise.Application.Tests.Features.Auth.Commands;

public class RegisterUserCommandHandlerTests
{
    private const string NomUtilisateur = "sung-jin-woo";
    private const string MotDePasse = "Éveil-du-Système-2026";

    private static readonly DateTimeOffset Inscription =
        new(2026, 7, 23, 9, 0, 0, TimeSpan.Zero);

    private readonly IUserRepository _chasseurs = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hacheur = Substitute.For<IPasswordHasher>();

    public RegisterUserCommandHandlerTests()
    {
        // Par défaut le nom est libre : chaque test qui teste le conflit le dit lui-même.
        _chasseurs.ExistsWithUsernameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _hacheur.Hash(Arg.Any<string>()).Returns(EmpreinteFactice);
    }

    // Une empreinte n'a pas à ressembler au secret : ce préfixe rend visible, à la lecture
    // d'un échec, laquelle des deux valeurs a été stockée.
    private const string EmpreinteFactice = "empreinte-de-Éveil-du-Système-2026";

    private RegisterUserCommandHandler Handler() =>
        new(_chasseurs, _hacheur, new HorlogeFigee(Inscription));

    private Task<RegisterUserResult> Inscrire(
        string nomUtilisateur = NomUtilisateur,
        string motDePasse = MotDePasse) =>
        Handler().Handle(
            new RegisterUserCommand(nomUtilisateur, motDePasse),
            CancellationToken.None);

    /// <summary>Le Chasseur passé à <c>AddAsync</c>, ou l'échec du test s'il n'y en a pas.</summary>
    private User ChasseurPersiste()
    {
        var appels = _chasseurs.ReceivedCalls()
            .Where(appel => appel.GetMethodInfo().Name == nameof(IUserRepository.AddAsync))
            .ToList();

        appels.Should().ContainSingle("le Chasseur doit être persisté une seule fois");

        return (User)appels[0].GetArguments()[0]!;
    }

    [Fact]
    public async Task Persiste_le_Chasseur_sous_le_nom_demande()
    {
        await Inscrire();

        ChasseurPersiste().Username.Should().Be(NomUtilisateur);
    }

    // Le cœur de la tâche : ce test doit rougir si quelqu'un branche un jour le mot de passe
    // brut sur PasswordHash.
    [Fact]
    public async Task Ne_stocke_jamais_le_mot_de_passe_en_clair()
    {
        await Inscrire();

        ChasseurPersiste().PasswordHash.Should().NotBe(MotDePasse);
    }

    [Fact]
    public async Task Stocke_l_empreinte_calculee_par_le_hacheur()
    {
        await Inscrire();

        ChasseurPersiste().PasswordHash.Should().Be(EmpreinteFactice);
    }

    [Fact]
    public async Task Soumet_le_mot_de_passe_recu_au_hacheur()
    {
        await Inscrire();

        _hacheur.Received(1).Hash(MotDePasse);
    }

    [Fact]
    public async Task Horodate_l_inscription_avec_l_horloge_fournie()
    {
        await Inscrire();

        ChasseurPersiste().RegisteredAt.Should().Be(Inscription);
    }

    [Fact]
    public async Task Renvoie_l_identifiant_du_Chasseur_inscrit()
    {
        var resultat = await Inscrire();

        resultat.UserId.Should().Be(ChasseurPersiste().Id);
    }

    [Fact]
    public async Task Renvoie_le_nom_du_Chasseur_inscrit()
    {
        (await Inscrire()).Username.Should().Be(NomUtilisateur);
    }

    [Fact]
    public async Task Refuse_un_nom_d_utilisateur_deja_pris()
    {
        _chasseurs.ExistsWithUsernameAsync(NomUtilisateur, Arg.Any<CancellationToken>())
            .Returns(true);

        var acte = () => Inscrire();

        await acte.Should().ThrowAsync<UsernameAlreadyTakenException>();
    }

    [Fact]
    public async Task Ne_persiste_rien_quand_le_nom_d_utilisateur_est_deja_pris()
    {
        _chasseurs.ExistsWithUsernameAsync(NomUtilisateur, Arg.Any<CancellationToken>())
            .Returns(true);

        var acte = () => Inscrire();

        await acte.Should().ThrowAsync<UsernameAlreadyTakenException>();
        await _chasseurs.DidNotReceive()
            .AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    // Le message remonte jusqu'à l'écran : règle non négociable n°7.
    [Fact]
    public async Task Explique_le_conflit_en_francais()
    {
        _chasseurs.ExistsWithUsernameAsync(NomUtilisateur, Arg.Any<CancellationToken>())
            .Returns(true);

        var acte = () => Inscrire();

        (await acte.Should().ThrowAsync<UsernameAlreadyTakenException>())
            .Which.Message.Should().Be(
                "Ce nom de Chasseur est déjà pris. Choisis-en un autre.");
    }

    // Le nom rogné est celui que porte le compte : chercher le brut laisserait passer
    // « sung » puis « sung  » comme deux inscriptions distinctes.
    [Fact]
    public async Task Cherche_le_conflit_sur_le_nom_rogne()
    {
        await Inscrire(nomUtilisateur: "  sung-jin-woo  ");

        await _chasseurs.Received(1)
            .ExistsWithUsernameAsync(NomUtilisateur, Arg.Any<CancellationToken>());
    }

    private sealed class HorlogeFigee(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }
}
