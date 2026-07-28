using Arise.Application.Common.Abstractions;
using Arise.Application.Features.Tasks.Queries.GetTasks;
using Arise.Domain.Tasks;
using FluentAssertions;
using NSubstitute;

namespace Arise.Application.Tests.Features.Tasks.Queries;

/// <summary>
/// La liste des tâches qui restent à faire.
///
/// <para>Le tri des tâches faites se décide ici et non dans le repository : c'est une règle
/// d'affichage — « ce qu'il te reste » — et la garder visible dans la couche Application la rend
/// éprouvable sans base. Même partage que <c>GetHabitsQuery</c> avec les archivées.</para>
/// </summary>
public class GetTasksQueryHandlerTests
{
    private static readonly DateTimeOffset Creation =
        new(2026, 7, 26, 9, 0, 0, TimeSpan.Zero);

    private readonly ITaskItemRepository _taches = Substitute.For<ITaskItemRepository>();

    private readonly Guid _chasseur = Guid.NewGuid();

    private TaskItem Declarer(
        string titre,
        DateOnly? echeance = null,
        DateTimeOffset? creation = null,
        bool faite = false)
    {
        var tache = TaskItem.Create(_chasseur, titre, echeance, creation ?? Creation);

        if (faite)
        {
            tache.Complete(Creation.AddHours(1));
        }

        return tache;
    }

    private void ListeDuChasseur(params TaskItem[] taches) =>
        _taches.GetForHunterAsync(_chasseur, Arg.Any<CancellationToken>()).Returns(taches);

    private Task<IReadOnlyList<TaskSummary>> Lire() =>
        new GetTasksQueryHandler(_taches).Handle(
            new GetTasksQuery(_chasseur), CancellationToken.None);

    [Fact]
    public async Task Rend_les_taches_a_faire_du_Chasseur()
    {
        ListeDuChasseur(Declarer("Appeler le dentiste"));

        (await Lire()).Should().ContainSingle()
            .Which.Title.Should().Be("Appeler le dentiste");
    }

    [Fact]
    public async Task Rend_l_identifiant_de_chaque_tache()
    {
        var tache = Declarer("Payer le loyer");
        ListeDuChasseur(tache);

        (await Lire()).Should().ContainSingle().Which.TaskId.Should().Be(tache.Id);
    }

    [Fact]
    public async Task Rend_l_echeance_de_chaque_tache()
    {
        ListeDuChasseur(Declarer("Payer le loyer", echeance: new DateOnly(2026, 8, 1)));

        (await Lire()).Should().ContainSingle()
            .Which.DueDate.Should().Be(new DateOnly(2026, 8, 1));
    }

    // Une tâche faite a quitté la liste : c'est tout l'intérêt de la cocher.
    [Fact]
    public async Task Ne_rend_pas_les_taches_deja_faites()
    {
        ListeDuChasseur(Declarer("Ranger le garage", faite: true));

        (await Lire()).Should().BeEmpty();
    }

    [Fact]
    public async Task Rend_une_liste_vide_pour_un_Chasseur_sans_tache()
    {
        ListeDuChasseur();

        (await Lire()).Should().BeEmpty();
    }

    // L'échéance la plus proche d'abord : c'est l'ordre dans lequel le Chasseur a besoin de lire
    // sa liste, et le retard remonte de lui-même en tête.
    [Fact]
    public async Task Ordonne_par_echeance_croissante()
    {
        ListeDuChasseur(
            Declarer("Plus tard", echeance: new DateOnly(2026, 8, 10)),
            Declarer("Bientôt", echeance: new DateOnly(2026, 8, 1)));

        (await Lire()).Select(tache => tache.Title)
            .Should().ContainInOrder("Bientôt", "Plus tard");
    }

    // Une tâche sans échéance n'est pas urgente : elle passe après celles que le Chasseur a
    // datées, plutôt que de squatter la tête de liste comme le ferait un tri naïf sur null.
    [Fact]
    public async Task Place_les_taches_sans_echeance_apres_celles_qui_en_ont()
    {
        ListeDuChasseur(
            Declarer("Sans date"),
            Declarer("Datée", echeance: new DateOnly(2026, 12, 31)));

        (await Lire()).Select(tache => tache.Title)
            .Should().ContainInOrder("Datée", "Sans date");
    }

    // Ordre stable : sans ce départage, la liste dépendrait de l'ordre des lignes rendu par
    // PostgreSQL et se réarrangerait d'un rafraîchissement à l'autre.
    [Fact]
    public async Task Departage_deux_echeances_egales_par_la_date_de_creation()
    {
        var echeance = new DateOnly(2026, 8, 1);
        ListeDuChasseur(
            Declarer("Ajoutée ensuite", echeance: echeance, creation: Creation.AddHours(2)),
            Declarer("Ajoutée d'abord", echeance: echeance, creation: Creation));

        (await Lire()).Select(tache => tache.Title)
            .Should().ContainInOrder("Ajoutée d'abord", "Ajoutée ensuite");
    }

    [Fact]
    public async Task Departage_deux_taches_sans_echeance_par_la_date_de_creation()
    {
        ListeDuChasseur(
            Declarer("Ajoutée ensuite", creation: Creation.AddHours(2)),
            Declarer("Ajoutée d'abord", creation: Creation));

        (await Lire()).Select(tache => tache.Title)
            .Should().ContainInOrder("Ajoutée d'abord", "Ajoutée ensuite");
    }
}
