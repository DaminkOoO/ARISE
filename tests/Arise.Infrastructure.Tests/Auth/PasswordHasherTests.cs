using Arise.Application.Common.Abstractions;
using Arise.Infrastructure.Auth;
using FluentAssertions;

namespace Arise.Infrastructure.Tests.Auth;

/// <summary>
/// Éprouve le hachage tel qu'ARISE l'utilise : une empreinte salée qu'on ne peut pas
/// rétro-comparer, et une vérification qui traite une empreinte corrompue en base comme un
/// échec d'authentification — pas comme une panne serveur (contrat <see cref="IPasswordHasher"/>).
/// On ne teste pas l'algorithme d'Identity : on teste notre adaptation de son contrat.
/// </summary>
public class PasswordHasherTests
{
    private const string MotDePasse = "Réveille-toi, Chasseur-42";

    private readonly IPasswordHasher hasher = new PasswordHasher();

    [Fact]
    public void Ne_rend_jamais_l_empreinte_egale_au_mot_de_passe_en_clair()
    {
        hasher.Hash(MotDePasse).Should().NotBe(MotDePasse);
    }

    // Le sel doit être aléatoire : deux empreintes du même mot de passe diffèrent, sinon une
    // table arc-en-ciel casse tous les comptes d'un coup.
    [Fact]
    public void Sale_chaque_empreinte_differemment()
    {
        hasher.Hash(MotDePasse).Should().NotBe(hasher.Hash(MotDePasse));
    }

    [Fact]
    public void Accepte_le_bon_mot_de_passe()
    {
        var empreinte = hasher.Hash(MotDePasse);

        hasher.Verify(MotDePasse, empreinte).Should().BeTrue();
    }

    [Fact]
    public void Rejette_un_mauvais_mot_de_passe()
    {
        var empreinte = hasher.Hash(MotDePasse);

        hasher.Verify("mauvais mot de passe", empreinte).Should().BeFalse();
    }

    // Une empreinte illisible en base est un échec d'authentification, pas une exception qui
    // remonte : Identity lève FormatException sur un base64 malformé, on doit l'absorber.
    [Fact]
    public void Rend_faux_sans_lever_sur_une_empreinte_corrompue()
    {
        var acte = () => hasher.Verify(MotDePasse, "ceci n'est pas une empreinte valide !!!");

        acte.Should().NotThrow();
        acte().Should().BeFalse();
    }
}
