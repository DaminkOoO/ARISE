using Arise.Domain.Hunters;
using FluentAssertions;

namespace Arise.Domain.Tests.Hunters;

public class HunterProfileTests
{
    [Fact]
    public void Create_demarre_au_niveau_1()
    {
        HunterProfile.Create().Level.Should().Be(1);
    }

    [Fact]
    public void Create_demarre_au_rang_E()
    {
        HunterProfile.Create().Rank.Should().Be(HunterRank.E);
    }

    [Fact]
    public void Create_demarre_sans_xp()
    {
        HunterProfile.Create().CurrentXp.Should().Be(0);
    }

    [Fact]
    public void Create_calcule_l_xp_requis_pour_le_niveau_2()
    {
        // XpToNextLevel(1) = 100 + (1 - 1) * 20 = 100 (doc mécaniques, section 1).
        HunterProfile.Create().XpToNextLevel.Should().Be(100);
    }

    [Fact]
    public void Create_demarre_sans_serie()
    {
        var profil = HunterProfile.Create();

        profil.StreakCurrent.Should().Be(0);
        profil.StreakLongest.Should().Be(0);
    }

    [Fact]
    public void Create_demarre_sans_derniere_completion()
    {
        HunterProfile.Create().LastCompletionDate.Should().BeNull();
    }

    [Fact]
    public void Create_attribue_un_identifiant_distinct_a_chaque_Chasseur()
    {
        var premier = HunterProfile.Create();
        var second = HunterProfile.Create();

        second.Id.Should().NotBe(premier.Id);
    }

    // --- RankFor ---------------------------------------------------------

    [Theory]
    [InlineData(1, HunterRank.E)]
    [InlineData(4, HunterRank.E)]
    [InlineData(5, HunterRank.D)]
    [InlineData(9, HunterRank.D)]
    [InlineData(10, HunterRank.C)]
    [InlineData(14, HunterRank.C)]
    [InlineData(15, HunterRank.B)]
    [InlineData(19, HunterRank.B)]
    [InlineData(20, HunterRank.A)]
    [InlineData(24, HunterRank.A)]
    [InlineData(25, HunterRank.S)]
    [InlineData(40, HunterRank.S)] // rang S sans plafond : le niveau continue de monter.
    public void RankFor_mappe_le_niveau_vers_le_bon_rang(int niveau, HunterRank rangAttendu)
    {
        HunterProfile.RankFor(niveau).Should().Be(rangAttendu);
    }

    [Fact]
    public void RankFor_refuse_un_niveau_inferieur_a_1()
    {
        var acte = () => HunterProfile.RankFor(0);

        acte.Should().Throw<ArgumentOutOfRangeException>();
    }

    // --- AwardXp -----------------------------------------------------------

    [Fact]
    public void AwardXp_cumule_l_xp_sans_franchir_de_niveau()
    {
        var profil = HunterProfile.Create();

        profil.AwardXp(60);

        profil.CurrentXp.Should().Be(60);
        profil.Level.Should().Be(1);
    }

    [Fact]
    public void AwardXp_a_la_frontiere_du_seuil_ne_fait_pas_monter_de_niveau()
    {
        var profil = HunterProfile.Create();

        // Seuil du niveau 1 : 100 XP. 99 XP reste au niveau 1.
        profil.AwardXp(99);

        profil.Level.Should().Be(1);
    }

    [Fact]
    public void AwardXp_au_seuil_exact_fait_monter_d_un_niveau()
    {
        var profil = HunterProfile.Create();

        profil.AwardXp(100);

        profil.Level.Should().Be(2);
    }

    [Fact]
    public void AwardXp_au_seuil_exact_consomme_tout_l_xp_du_niveau()
    {
        var profil = HunterProfile.Create();

        profil.AwardXp(100);

        profil.CurrentXp.Should().Be(0);
    }

    [Fact]
    public void AwardXp_au_dela_du_seuil_conserve_le_reliquat_d_xp()
    {
        var profil = HunterProfile.Create();

        profil.AwardXp(110);

        profil.Level.Should().Be(2);
        profil.CurrentXp.Should().Be(10);
    }

    [Fact]
    public void AwardXp_recalcule_l_xp_requis_pour_le_nouveau_niveau()
    {
        var profil = HunterProfile.Create();

        profil.AwardXp(100);

        // XpToNextLevel(2) = 100 + (2 - 1) * 20 = 120.
        profil.XpToNextLevel.Should().Be(120);
    }

    [Fact]
    public void AwardXp_un_gros_gain_fait_monter_plusieurs_niveaux_d_un_coup()
    {
        var profil = HunterProfile.Create();

        // Niveau 1->2 : 100 XP. Niveau 2->3 : 120 XP. Niveau 3->4 : 140 XP. Total 360.
        // 400 XP doit donc atteindre le niveau 4 avec 40 XP de reliquat.
        profil.AwardXp(400);

        profil.Level.Should().Be(4);
        profil.CurrentXp.Should().Be(40);
    }

    [Fact]
    public void AwardXp_refuse_un_montant_negatif()
    {
        var profil = HunterProfile.Create();

        var acte = () => profil.AwardXp(-10);

        acte.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AwardXp_refuse_un_montant_nul()
    {
        var profil = HunterProfile.Create();

        var acte = () => profil.AwardXp(0);

        acte.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AwardXp_ne_leve_aucun_evenement_tant_que_le_rang_ne_change_pas()
    {
        var profil = HunterProfile.Create();

        // Niveau 1 -> 2 reste dans le rang E (E couvre les niveaux 1 à 4).
        profil.AwardXp(100);

        profil.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AwardXp_leve_HunterRankedUpEvent_au_franchissement_du_rang_D()
    {
        var profil = HunterProfile.Create();

        // Niveau 4 -> 5 franchit E -> D. Cumul XP niveaux 1 à 4 : 100+120+140+160 = 520.
        profil.AwardXp(520);

        profil.Level.Should().Be(5);
        profil.DomainEvents.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new HunterRankedUpEvent(profil.Id, HunterRank.E, HunterRank.D));
    }

    [Fact]
    public void AwardXp_leve_un_evenement_par_rang_franchi_quand_plusieurs_rangs_sont_traverses()
    {
        var profil = HunterProfile.Create();

        // Un seul gros gain qui traverse le rang D (5-9) jusqu'en plein rang C (10-14) : la
        // boucle « tant que » du doc mécaniques publie un HunterRankedUpEvent à chaque
        // franchissement rencontré, pas seulement au dernier — un par transition de rang.
        var cumulJusquAuNiveau12 = Enumerable.Range(1, 11).Sum(niveau => 100 + ((niveau - 1) * 20));
        profil.AwardXp(cumulJusquAuNiveau12 + 50);

        profil.Level.Should().Be(12);
        profil.DomainEvents.Should().BeEquivalentTo(
            [
                new HunterRankedUpEvent(profil.Id, HunterRank.E, HunterRank.D),
                new HunterRankedUpEvent(profil.Id, HunterRank.D, HunterRank.C),
            ],
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void ClearDomainEvents_vide_les_evenements_accumules()
    {
        var profil = HunterProfile.Create();
        profil.AwardXp(520); // franchit le rang D.

        profil.ClearDomainEvents();

        profil.DomainEvents.Should().BeEmpty();
    }

    // --- RegisterDailyCompletion --------------------------------------------

    [Fact]
    public void RegisterDailyCompletion_premiere_completion_demarre_une_serie_de_1()
    {
        var profil = HunterProfile.Create();
        var aujourdHui = new DateOnly(2026, 7, 24);

        profil.RegisterDailyCompletion(aujourdHui);

        profil.StreakCurrent.Should().Be(1);
        profil.LastCompletionDate.Should().Be(aujourdHui);
    }

    [Fact]
    public void RegisterDailyCompletion_le_jour_suivant_immediat_incremente_la_serie()
    {
        var profil = HunterProfile.Create();
        var hier = new DateOnly(2026, 7, 23);
        var aujourdHui = new DateOnly(2026, 7, 24);
        profil.RegisterDailyCompletion(hier);

        profil.RegisterDailyCompletion(aujourdHui);

        profil.StreakCurrent.Should().Be(2);
    }

    [Fact]
    public void RegisterDailyCompletion_deux_fois_le_meme_jour_n_incremente_pas_la_serie()
    {
        var profil = HunterProfile.Create();
        var aujourdHui = new DateOnly(2026, 7, 24);
        profil.RegisterDailyCompletion(aujourdHui);

        profil.RegisterDailyCompletion(aujourdHui);

        profil.StreakCurrent.Should().Be(1);
    }

    [Fact]
    public void RegisterDailyCompletion_un_trou_de_deux_jours_ou_plus_repart_de_1()
    {
        var profil = HunterProfile.Create();
        var ilYA3Jours = new DateOnly(2026, 7, 21);
        var aujourdHui = new DateOnly(2026, 7, 24);
        profil.RegisterDailyCompletion(ilYA3Jours);

        profil.RegisterDailyCompletion(aujourdHui);

        profil.StreakCurrent.Should().Be(1);
    }

    [Fact]
    public void RegisterDailyCompletion_met_a_jour_le_record_de_la_plus_longue_serie()
    {
        var profil = HunterProfile.Create();
        profil.RegisterDailyCompletion(new DateOnly(2026, 7, 22));
        profil.RegisterDailyCompletion(new DateOnly(2026, 7, 23));
        profil.RegisterDailyCompletion(new DateOnly(2026, 7, 24));

        profil.StreakLongest.Should().Be(3);
    }

    [Fact]
    public void RegisterDailyCompletion_le_record_ne_redescend_pas_apres_une_serie_rompue()
    {
        var profil = HunterProfile.Create();
        profil.RegisterDailyCompletion(new DateOnly(2026, 7, 20));
        profil.RegisterDailyCompletion(new DateOnly(2026, 7, 21));
        profil.RegisterDailyCompletion(new DateOnly(2026, 7, 22)); // série de 3, record = 3.

        profil.RegisterDailyCompletion(new DateOnly(2026, 7, 25)); // trou → série repart à 1.

        profil.StreakCurrent.Should().Be(1);
        profil.StreakLongest.Should().Be(3);
    }

    // --- CheckStreakBreak ----------------------------------------------------

    [Fact]
    public void CheckStreakBreak_sans_aucune_completion_ne_rompt_rien()
    {
        var profil = HunterProfile.Create();

        var rompue = profil.CheckStreakBreak(new DateOnly(2026, 7, 24));

        rompue.Should().BeFalse();
    }

    [Fact]
    public void CheckStreakBreak_derniere_completion_hier_ne_rompt_pas_la_serie()
    {
        var profil = HunterProfile.Create();
        profil.RegisterDailyCompletion(new DateOnly(2026, 7, 23));

        var rompue = profil.CheckStreakBreak(new DateOnly(2026, 7, 24));

        rompue.Should().BeFalse();
        profil.StreakCurrent.Should().Be(1);
    }

    [Fact]
    public void CheckStreakBreak_derniere_completion_aujourd_hui_ne_rompt_pas_la_serie()
    {
        var profil = HunterProfile.Create();
        var aujourdHui = new DateOnly(2026, 7, 24);
        profil.RegisterDailyCompletion(aujourdHui);

        var rompue = profil.CheckStreakBreak(aujourdHui);

        rompue.Should().BeFalse();
        profil.StreakCurrent.Should().Be(1);
    }

    [Fact]
    public void CheckStreakBreak_un_jour_entier_sans_completion_rompt_la_serie()
    {
        var profil = HunterProfile.Create();
        profil.RegisterDailyCompletion(new DateOnly(2026, 7, 22));

        // Dernière complétion il y a deux jours, aucun rappel entre-temps : la série casse.
        var rompue = profil.CheckStreakBreak(new DateOnly(2026, 7, 24));

        rompue.Should().BeTrue();
        profil.StreakCurrent.Should().Be(0);
    }

    [Fact]
    public void CheckStreakBreak_ne_rompt_pas_deux_fois_une_serie_deja_a_zero()
    {
        var profil = HunterProfile.Create();
        profil.RegisterDailyCompletion(new DateOnly(2026, 7, 22));
        profil.CheckStreakBreak(new DateOnly(2026, 7, 24)); // première rupture, StreakCurrent = 0.

        var rompueDeNouveau = profil.CheckStreakBreak(new DateOnly(2026, 7, 25));

        rompueDeNouveau.Should().BeFalse();
    }
}
