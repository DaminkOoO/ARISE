using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Arise.Api.Tests;

/// <summary>
/// Parcours Habitudes &amp; Tâches de bout en bout : inscription, éveil, puis les endpoints de
/// domaine, sur le vrai hôte par-dessus un vrai Postgres.
///
/// <para>Le groupe de tests le plus important de ce fichier est <b>l'isolation entre
/// Chasseurs</b>. Les endpoints déduisent le profil du jeton et n'acceptent aucun identifiant de
/// profil dans le corps : c'est ce qui empêche un Chasseur authentifié de journaliser les
/// habitudes d'un autre ou de cocher ses tâches. Une régression y serait invisible à l'écran et
/// silencieuse en production.</para>
/// </summary>
[Collection(ApiCollection.Nom)]
public class HabitudesEtTachesEndpointsTests(ApiFixture api)
{
    private const string MotDePasse = "Ombre-Monarque-2026";

    private const string Fuseau = "Europe/Paris";

    private static string NomUnique(string racine) => $"{racine}{Guid.NewGuid():N}"[..12];

    /// <summary>Un client déjà porteur du jeton d'un Chasseur inscrit mais <b>pas</b> éveillé.</summary>
    private async Task<HttpClient> ChasseurInscrit(string racine)
    {
        var client = api.CreateClient();
        var nom = NomUnique(racine);

        await client.PostAsJsonAsync(
            "/auth/register", new { Username = nom, Password = MotDePasse });

        var connexion = await client.PostAsJsonAsync(
            "/auth/login", new { Username = nom, Password = MotDePasse });

        var jeton = (await connexion.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jeton);

        return client;
    }

    /// <summary>Un client porteur du jeton d'un Chasseur éveillé, donc doté d'un profil.</summary>
    private async Task<HttpClient> ChasseurEveille(string racine)
    {
        var client = await ChasseurInscrit(racine);

        var eveil = await client.PostAsJsonAsync(
            "/hunters/eveil", new { Objectifs = new[] { "Habitudes" } });

        eveil.StatusCode.Should().Be(HttpStatusCode.Created);

        return client;
    }

    private static async Task<Guid> IdentifiantRendu(HttpResponseMessage reponse, string propriete)
    {
        var corps = await reponse.Content.ReadFromJsonAsync<JsonElement>();

        return corps.GetProperty(propriete).GetGuid();
    }

    // --- Éveil ---------------------------------------------------------------------------

    [Fact]
    public async Task Eveille_un_Chasseur_inscrit()
    {
        var client = await ChasseurInscrit("Sung");

        var reponse = await client.PostAsJsonAsync(
            "/hunters/eveil", new { Objectifs = new[] { "Sport" } });

        reponse.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // Un second éveil écraserait toute la progression : l'ancien profil deviendrait
    // inatteignable, XP et séries compris.
    [Fact]
    public async Task Refuse_un_second_eveil_en_409()
    {
        var client = await ChasseurEveille("Igris");

        var reponse = await client.PostAsJsonAsync(
            "/hunters/eveil", new { Objectifs = new[] { "Sport" } });

        reponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // 403 et non 401 : le Chasseur est bien authentifié, c'est son éveil qui manque.
    [Fact]
    public async Task Refuse_l_acces_aux_habitudes_a_un_compte_non_eveille_en_403()
    {
        var client = await ChasseurInscrit("Beru");

        var reponse = await client.GetAsync("/habitudes");

        reponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Explique_en_francais_qu_il_faut_s_eveiller()
    {
        var client = await ChasseurInscrit("Tusk");

        var reponse = await client.GetAsync("/habitudes");

        (await reponse.Content.ReadAsStringAsync()).Should().Contain("éveil");
    }

    // --- Habitudes ------------------------------------------------------------------------

    [Fact]
    public async Task Declare_une_habitude()
    {
        var client = await ChasseurEveille("Kaisel");

        var reponse = await client.PostAsJsonAsync(
            "/habitudes", new { Name = "Boire deux litres d'eau", Frequency = "Quotidienne" });

        reponse.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Relit_l_habitude_declaree()
    {
        var client = await ChasseurEveille("Iron");

        await client.PostAsJsonAsync(
            "/habitudes", new { Name = "Lire vingt minutes", Frequency = "Quotidienne" });

        var reponse = await client.GetAsync("/habitudes");
        var corps = await reponse.Content.ReadAsStringAsync();

        corps.Should().Contain("Lire vingt minutes");
    }

    [Fact]
    public async Task Refuse_une_habitude_homonyme_en_409()
    {
        var client = await ChasseurEveille("Greed");

        await client.PostAsJsonAsync(
            "/habitudes", new { Name = "Courir le matin", Frequency = "Quotidienne" });

        var reponse = await client.PostAsJsonAsync(
            "/habitudes", new { Name = "courir le matin", Frequency = "Quotidienne" });

        reponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Refuse_une_habitude_sans_nom_en_400()
    {
        var client = await ChasseurEveille("Tank");

        var reponse = await client.PostAsJsonAsync(
            "/habitudes", new { Name = "   ", Frequency = "Quotidienne" });

        reponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Le parcours complet : déclarer, journaliser, et lire la série que le backend calcule —
    // celle que l'app affichera sans jamais la recalculer.
    [Fact]
    public async Task Journalise_une_habitude_et_ouvre_sa_serie()
    {
        var client = await ChasseurEveille("Igru");

        var creation = await client.PostAsJsonAsync(
            "/habitudes", new { Name = "Méditer cinq minutes", Frequency = "Quotidienne" });
        var habitude = await IdentifiantRendu(creation, "habitId");

        var reponse = await client.PostAsJsonAsync(
            $"/habitudes/{habitude}/journal", new { FuseauHoraire = Fuseau });

        var corps = await reponse.Content.ReadFromJsonAsync<JsonElement>();

        reponse.StatusCode.Should().Be(HttpStatusCode.OK);
        corps.GetProperty("serieActuelle").GetInt32().Should().Be(1);
    }

    // Le barème d'engagement, réellement accordé : une habitude quotidienne vaut 3 XP
    // (doc mécaniques, section 1).
    [Fact]
    public async Task Accorde_trois_XP_pour_une_habitude_quotidienne()
    {
        var client = await ChasseurEveille("Jinah");

        var creation = await client.PostAsJsonAsync(
            "/habitudes", new { Name = "Étirements du soir", Frequency = "Quotidienne" });
        var habitude = await IdentifiantRendu(creation, "habitId");

        var reponse = await client.PostAsJsonAsync(
            $"/habitudes/{habitude}/journal", new { FuseauHoraire = Fuseau });

        var corps = await reponse.Content.ReadFromJsonAsync<JsonElement>();

        corps.GetProperty("xpAcquis").GetInt32().Should().Be(3);
    }

    // Double-tap : l'habitude reste tenue, mais le gain ne se rejoue pas.
    [Fact]
    public async Task N_accorde_pas_deux_fois_l_XP_du_meme_jour()
    {
        var client = await ChasseurEveille("Esil");

        var creation = await client.PostAsJsonAsync(
            "/habitudes", new { Name = "Ranger le bureau", Frequency = "Quotidienne" });
        var habitude = await IdentifiantRendu(creation, "habitId");

        await client.PostAsJsonAsync(
            $"/habitudes/{habitude}/journal", new { FuseauHoraire = Fuseau });
        var reponse = await client.PostAsJsonAsync(
            $"/habitudes/{habitude}/journal", new { FuseauHoraire = Fuseau });

        var corps = await reponse.Content.ReadFromJsonAsync<JsonElement>();

        corps.GetProperty("dejaJournalisee").GetBoolean().Should().BeTrue();
        corps.GetProperty("xpAcquis").GetInt32().Should().Be(0);
    }

    // --- Tâches ---------------------------------------------------------------------------

    [Fact]
    public async Task Declare_une_tache()
    {
        var client = await ChasseurEveille("Yoo");

        var reponse = await client.PostAsJsonAsync(
            "/taches", new { Title = "Appeler le dentiste", DueDate = (string?)null });

        reponse.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Relit_la_tache_a_faire()
    {
        var client = await ChasseurEveille("Cha");

        await client.PostAsJsonAsync(
            "/taches", new { Title = "Envoyer les documents", DueDate = "2026-08-01" });

        var corps = await (await client.GetAsync("/taches")).Content.ReadAsStringAsync();

        corps.Should().Contain("Envoyer les documents");
    }

    [Fact]
    public async Task Coche_une_tache_et_accorde_cinq_XP()
    {
        var client = await ChasseurEveille("Baek");

        var creation = await client.PostAsJsonAsync(
            "/taches", new { Title = "Payer le loyer", DueDate = (string?)null });
        var tache = await IdentifiantRendu(creation, "taskId");

        var reponse = await client.PostAsJsonAsync(
            $"/taches/{tache}/completion", new { FuseauHoraire = Fuseau });

        var corps = await reponse.Content.ReadFromJsonAsync<JsonElement>();

        reponse.StatusCode.Should().Be(HttpStatusCode.OK);
        corps.GetProperty("xpAcquis").GetInt32().Should().Be(5);
    }

    // Une tâche cochée quitte la liste : c'est tout l'intérêt de la cocher.
    [Fact]
    public async Task Une_tache_cochee_quitte_la_liste()
    {
        var client = await ChasseurEveille("Hwang");

        var creation = await client.PostAsJsonAsync(
            "/taches", new { Title = "Sortir les poubelles", DueDate = (string?)null });
        var tache = await IdentifiantRendu(creation, "taskId");

        await client.PostAsJsonAsync(
            $"/taches/{tache}/completion", new { FuseauHoraire = Fuseau });

        var corps = await (await client.GetAsync("/taches")).Content.ReadAsStringAsync();

        corps.Should().NotContain("Sortir les poubelles");
    }

    [Fact]
    public async Task Refuse_de_cocher_une_tache_inconnue_en_404()
    {
        var client = await ChasseurEveille("Min");

        var reponse = await client.PostAsJsonAsync(
            $"/taches/{Guid.NewGuid()}/completion", new { FuseauHoraire = Fuseau });

        reponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Isolation entre Chasseurs ----------------------------------------------------------
    //
    // Le groupe qui compte. Le profil se déduit du jeton et n'est jamais annoncé par l'appelant ;
    // ces tests le prouvent depuis l'extérieur, là où une revue de code ne verrait qu'un
    // enregistrement de plus dans un corps de requête.

    [Fact]
    public async Task Ne_montre_pas_les_habitudes_d_un_autre_Chasseur()
    {
        var premier = await ChasseurEveille("Alpha");
        var second = await ChasseurEveille("Bravo");

        await premier.PostAsJsonAsync(
            "/habitudes", new { Name = "Secret du premier", Frequency = "Quotidienne" });

        var corps = await (await second.GetAsync("/habitudes")).Content.ReadAsStringAsync();

        corps.Should().NotContain("Secret du premier");
    }

    [Fact]
    public async Task Ne_montre_pas_les_taches_d_un_autre_Chasseur()
    {
        var premier = await ChasseurEveille("Charlie");
        var second = await ChasseurEveille("Delta");

        await premier.PostAsJsonAsync(
            "/taches", new { Title = "Tâche du premier", DueDate = (string?)null });

        var corps = await (await second.GetAsync("/taches")).Content.ReadAsStringAsync();

        corps.Should().NotContain("Tâche du premier");
    }

    // Le cas qui justifie toute la liaison compte↔profil : connaître l'identifiant d'une
    // habitude d'autrui ne doit donner aucune prise dessus.
    [Fact]
    public async Task Refuse_de_journaliser_l_habitude_d_un_autre_Chasseur_en_404()
    {
        var premier = await ChasseurEveille("Echo");
        var second = await ChasseurEveille("Foxtrot");

        var creation = await premier.PostAsJsonAsync(
            "/habitudes", new { Name = "Habitude convoitée", Frequency = "Quotidienne" });
        var habitude = await IdentifiantRendu(creation, "habitId");

        var reponse = await second.PostAsJsonAsync(
            $"/habitudes/{habitude}/journal", new { FuseauHoraire = Fuseau });

        reponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Refuse_de_cocher_la_tache_d_un_autre_Chasseur_en_404()
    {
        var premier = await ChasseurEveille("Golf");
        var second = await ChasseurEveille("Hotel");

        var creation = await premier.PostAsJsonAsync(
            "/taches", new { Title = "Tâche convoitée", DueDate = (string?)null });
        var tache = await IdentifiantRendu(creation, "taskId");

        var reponse = await second.PostAsJsonAsync(
            $"/taches/{tache}/completion", new { FuseauHoraire = Fuseau });

        reponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Deux Chasseurs peuvent porter la même habitude : l'unicité est par Chasseur, pas globale.
    [Fact]
    public async Task Deux_Chasseurs_peuvent_suivre_la_meme_habitude()
    {
        var premier = await ChasseurEveille("India");
        var second = await ChasseurEveille("Juliett");

        await premier.PostAsJsonAsync(
            "/habitudes", new { Name = "Boire un thé", Frequency = "Quotidienne" });

        var reponse = await second.PostAsJsonAsync(
            "/habitudes", new { Name = "Boire un thé", Frequency = "Quotidienne" });

        reponse.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // --- Sans jeton --------------------------------------------------------------------------

    [Theory]
    [InlineData("/habitudes")]
    [InlineData("/taches")]
    public async Task Refuse_l_acces_sans_jeton_en_401(string route)
    {
        var reponse = await api.CreateClient().GetAsync(route);

        reponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
