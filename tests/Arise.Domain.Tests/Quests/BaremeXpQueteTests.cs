using Arise.Domain.Quests;
using FluentAssertions;

namespace Arise.Domain.Tests.Quests;

/// <summary>
/// Le barème d'XP par difficulté (doc mécaniques, section 1). Seule définition de ces bornes
/// dans le dépôt : l'agent s'en sert pour rejeter une récompense incohérente rendue par le
/// modèle, et <see cref="Quest"/> pour refuser d'exister avec une telle récompense.
///
/// <para>Les bornes se chevauchent volontairement (facile 10-15, moyenne 15-25) : 15 XP est
/// acceptable pour l'une comme pour l'autre, ce que ces tests figent explicitement plutôt que
/// de laisser un futur « corrigeur » resserrer les bornes au premier doute.</para>
/// </summary>
public class BaremeXpQueteTests
{
    [Theory]
    [InlineData(QuestDifficulty.Facile, 10)]
    [InlineData(QuestDifficulty.Facile, 15)]
    [InlineData(QuestDifficulty.Moyenne, 15)]
    [InlineData(QuestDifficulty.Moyenne, 25)]
    [InlineData(QuestDifficulty.Difficile, 25)]
    [InlineData(QuestDifficulty.Difficile, 40)]
    public void Accepte_une_recompense_dans_la_fourchette_de_sa_difficulte(
        QuestDifficulty difficulte, int xp)
    {
        BaremeXpQuete.EstCoherent(QuestType.Quotidienne, difficulte, xp).Should().BeTrue();
    }

    [Theory]
    [InlineData(QuestDifficulty.Facile, 9)]
    [InlineData(QuestDifficulty.Facile, 16)]
    [InlineData(QuestDifficulty.Moyenne, 14)]
    [InlineData(QuestDifficulty.Moyenne, 26)]
    [InlineData(QuestDifficulty.Difficile, 24)]
    [InlineData(QuestDifficulty.Difficile, 41)]
    public void Rejette_une_recompense_hors_de_la_fourchette_de_sa_difficulte(
        QuestDifficulty difficulte, int xp)
    {
        BaremeXpQuete.EstCoherent(QuestType.Quotidienne, difficulte, xp).Should().BeFalse();
    }

    // Une quête de pénalité vaut 10 XP fixes, « toujours facile par conception » : la
    // difficulté annoncée par le modèle ne change rien à la récompense attendue.
    [Theory]
    [InlineData(QuestDifficulty.Facile)]
    [InlineData(QuestDifficulty.Moyenne)]
    [InlineData(QuestDifficulty.Difficile)]
    public void Accepte_dix_XP_pour_une_quete_de_penalite_quelle_qu_en_soit_la_difficulte(
        QuestDifficulty difficulte)
    {
        BaremeXpQuete.EstCoherent(QuestType.Penalite, difficulte, 10).Should().BeTrue();
    }

    [Theory]
    [InlineData(9)]
    [InlineData(11)]
    [InlineData(40)]
    public void Rejette_toute_autre_recompense_pour_une_quete_de_penalite(int xp)
    {
        BaremeXpQuete.EstCoherent(QuestType.Penalite, QuestDifficulty.Difficile, xp)
            .Should().BeFalse();
    }

    [Fact]
    public void Rejette_une_recompense_nulle()
    {
        BaremeXpQuete.EstCoherent(QuestType.Quotidienne, QuestDifficulty.Facile, 0)
            .Should().BeFalse();
    }

    [Fact]
    public void Rejette_une_recompense_negative()
    {
        BaremeXpQuete.EstCoherent(QuestType.Quotidienne, QuestDifficulty.Facile, -10)
            .Should().BeFalse();
    }

    [Fact]
    public void Expose_la_fourchette_d_une_difficulte_pour_la_rappeler_au_modele()
    {
        BaremeXpQuete.Fourchette(QuestType.Quotidienne, QuestDifficulty.Moyenne)
            .Should().Be((15, 25));
    }

    [Fact]
    public void Expose_dix_XP_fixes_comme_fourchette_d_une_quete_de_penalite()
    {
        BaremeXpQuete.Fourchette(QuestType.Penalite, QuestDifficulty.Difficile)
            .Should().Be((10, 10));
    }
}
