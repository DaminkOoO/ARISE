using Arise.Domain.Habits;
using Arise.Domain.Hunters;
using FluentAssertions;

namespace Arise.Domain.Tests.Hunters;

/// <summary>
/// Le barème d'XP des gestes d'engagement — habitudes tenues, tâches cochées (doc mécaniques,
/// section 1). Distinct de <c>BaremeXpQuete</c> : ces montants sont fixes et ne sortent d'aucun
/// agent.
/// </summary>
public class BaremeXpEngagementTests
{
    [Fact]
    public void Une_habitude_quotidienne_vaut_trois_XP()
    {
        BaremeXpEngagement.PourHabitude(HabitFrequency.Quotidienne).Should().Be(3);
    }

    // Un engagement unique, pas sept petits : la payer comme une quotidienne rendrait le rythme
    // hebdomadaire strictement perdant, et personne ne le choisirait.
    [Fact]
    public void Une_habitude_hebdomadaire_vaut_plus_qu_une_quotidienne()
    {
        BaremeXpEngagement.PourHabitude(HabitFrequency.Hebdomadaire)
            .Should().BeGreaterThan(BaremeXpEngagement.PourHabitude(HabitFrequency.Quotidienne));
    }

    [Fact]
    public void Une_habitude_hebdomadaire_vaut_dix_XP()
    {
        BaremeXpEngagement.PourHabitude(HabitFrequency.Hebdomadaire).Should().Be(10);
    }

    [Fact]
    public void Une_tache_vaut_cinq_XP()
    {
        BaremeXpEngagement.PourTache.Should().Be(5);
    }

    // Le plafond est la seule protection contre la ferme d'XP : le nombre de quêtes est fixé par
    // le Système, celui des habitudes et des tâches par le Chasseur lui-même.
    [Fact]
    public void Le_plafond_quotidien_est_de_vingt_cinq_XP()
    {
        BaremeXpEngagement.PlafondQuotidien.Should().Be(25);
    }

    // Il doit rester une garniture face aux 60–100 XP/jour des quêtes : au-delà, le rang S
    // arriverait bien plus tôt que les ~3 mois annoncés par le document.
    [Fact]
    public void Le_plafond_reste_inferieur_au_rythme_quotidien_des_quetes()
    {
        BaremeXpEngagement.PlafondQuotidien.Should().BeLessThan(60);
    }

    [Fact]
    public void Accorde_la_valeur_entiere_d_un_geste_quand_rien_n_a_ete_acquis()
    {
        BaremeXpEngagement.Accordable(valeurDuGeste: 10, dejaAcquisAujourdHui: 0)
            .Should().Be(10);
    }

    [Fact]
    public void Accorde_la_valeur_entiere_tant_que_le_plafond_n_est_pas_atteint()
    {
        BaremeXpEngagement.Accordable(valeurDuGeste: 5, dejaAcquisAujourdHui: 10)
            .Should().Be(5);
    }

    // Le geste n'est pas refusé pour autant : l'habitude est tenue, la tâche est faite. Seul le
    // gain est rogné — et c'est ce qui permet à l'écran de dire « plafond atteint » plutôt que
    // de faire échouer le geste.
    [Fact]
    public void Rogne_le_gain_qui_deborderait_du_plafond()
    {
        BaremeXpEngagement.Accordable(valeurDuGeste: 10, dejaAcquisAujourdHui: 20)
            .Should().Be(5);
    }

    [Fact]
    public void N_accorde_plus_rien_une_fois_le_plafond_atteint()
    {
        BaremeXpEngagement.Accordable(valeurDuGeste: 5, dejaAcquisAujourdHui: 25)
            .Should().Be(0);
    }

    // Le total du jour est recalculé, jamais stocké : un décompte qui dériverait au-delà du
    // plafond ne doit pas produire un gain négatif, qui retirerait de l'XP au Chasseur.
    [Fact]
    public void N_accorde_jamais_un_gain_negatif()
    {
        BaremeXpEngagement.Accordable(valeurDuGeste: 5, dejaAcquisAujourdHui: 40)
            .Should().Be(0);
    }

    [Fact]
    public void Refuse_un_rythme_d_habitude_inconnu()
    {
        var acte = () => BaremeXpEngagement.PourHabitude((HabitFrequency)99);

        acte.Should().Throw<ArgumentOutOfRangeException>();
    }
}
