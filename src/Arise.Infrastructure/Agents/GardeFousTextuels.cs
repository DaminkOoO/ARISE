using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Arise.Infrastructure.Agents;

/// <summary>
/// Les garde-fous de <b>texte</b> communs à tous les agents du Système : langue, reproche,
/// vocabulaire médical, prescription chiffrée. Ils sont ici — et pas recopiés dans chaque agent
/// — parce que ce sont des règles produit non négociables (n°5 et n°7), pas des détails de
/// l'agent qui les applique. Une copie par agent, c'est une copie qui rate la prochaine
/// formulation à bannir pendant que les autres l'attrapent.
///
/// <para>Ce qui reste <b>propre à chaque agent</b> n'est pas ici : les bornes de magnitude d'une
/// quête de sport, la fourchette d'XP, les rythmes d'habitude. Cette classe ne connaît que du
/// texte.</para>
///
/// <para>Tous les prédicats sauf <see cref="EstVisiblementAnglais"/> et
/// <see cref="SembleEtreEnFrancais"/> attendent un texte déjà passé par
/// <see cref="SansAccents"/> : les lexiques sont écrits sans accents pour qu'un mot mal accentué
/// ne les contourne pas.</para>
/// </summary>
internal static class GardeFousTextuels
{
    /// <summary>
    /// Prescription chiffrée : charge, allure, calories, pourcentage d'effort. Interdites par la
    /// règle non négociable n°5.
    ///
    /// <para>Ce qui n'est <b>pas</b> visé, et volontairement : les répétitions et les minutes au
    /// poids du corps (« 40 pompes », « 5 minutes de gainage »). C'est l'exemple validé du
    /// document de référence, et un filtre qui le rejetterait servirait le repli tous les jours
    /// — une dégradation permanente au nom d'un garde-fou.</para>
    /// </summary>
    private static readonly Regex PrescriptionChiffree = new(
        @"\d+\s*(?:%|kg\b|kilos?\b|kilogrammes?\b|lbs?\b|livres?\b|kcal\b|calories?\b|rm\b"
        + @"|watts?\b|km\s*/\s*h\b|min\s*/\s*km\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Les unités et les tournures de prescription, jugées <b>indépendamment du chiffre</b>.
    /// Exiger un <c>\d+</c> collé à l'unité se contournait sans effort : « à cent kilos » écrit
    /// en toutes lettres, « g de protéines » (un dosage nutritionnel, nommé par la règle n°5),
    /// « 4:30 au kilomètre » où le séparateur porte l'allure.
    ///
    /// <para>« jeûne » et « jeune » se confondent une fois les accents retirés : un « reste
    /// jeune » innocent serait rejeté. C'est le bon sens du compromis — un faux positif ne coûte
    /// qu'un repli sûr, là où un faux négatif atteint l'écran du Chasseur.</para>
    /// </summary>
    private static readonly Regex UniteDePrescription = new(
        @"\b(?:kg|kilos?|kilogrammes?|kcal|calories?|watts?|bpm|pulsations?|jeune|jeuner"
        + @"|allures?)\b|\bg\s+de\s+proteines?\b|\bau\s+kilometre\b|\d\s*:\s*\d|%",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Vocabulaire qu'un texte du Système n'a aucune raison d'employer : diagnostic et
    /// interprétation de symptôme, registre médical, et reproche.
    ///
    /// <para><b>« douleur » et « blessure » n'y figurent pas</b>, et c'est délibéré : la règle
    /// n°5 <i>exige</i> qu'une mention de douleur renvoie vers un professionnel de santé. Les
    /// bannir rejetterait « arrête-toi à la moindre douleur et consulte un professionnel de
    /// santé » — la plus sûre des réponses. Ce qui est interdit est l'injonction à passer outre :
    /// voir <see cref="InviteAPasserOutreLaDouleur"/>.</para>
    /// </summary>
    private static readonly Regex VocabulaireInterdit = new(
        @"\b(?:tendinite|entorse|claquage|dechirure|fracture"
        + @"|diagnostic|symptome|pathologie|inflammation|anti-?inflammatoire|ordonnance"
        + @"|posologie|dose|medicament|traitement|therapie|regime"
        + @"|honte|paresse|paresseux|paresseuse|faineant|mediocre|lamentable|indigne"
        + @"|decevant|decu|echec|echoue|echouee)s?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Le reproche implicite — celui qui ne dit ni « honte » ni « médiocre », et qui est pourtant
    /// le registre qu'un modèle adopte quand on lui transmet une série à zéro, c'est-à-dire le
    /// jour où le Chasseur a déjà décroché (règle n°5).
    ///
    /// <para>« n'abandonne pas », pourtant bien intentionné, tombe sous ce filtre : le repli qui
    /// en résulte reste un texte accueillant, là où un reproche laissé passer atteint quelqu'un
    /// un mauvais jour.</para>
    /// </summary>
    private static readonly Regex ReprocheImplicite = new(
        @"\babandonn\w*|\bta faute\b|\bnuls?\b|\bminable|\bpitoyable|\bpathetique"
        + @"|\btu n'as pas\b|\bencore une fois\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// L'injonction à poursuivre malgré un signal du corps — le vrai danger de la règle n°5, là
    /// où le mot « douleur » seul n'en est pas un.
    /// </summary>
    private static readonly Regex InjonctionAPasserOutre = new(
        @"\b(?:malgre|surmonte|surmonter|surmontant|ignore|ignorer|ignorant|depasse|depasser"
        + @"|encaisse|encaisser|endure|endurer|passer?\s+outre|sans\s+ecouter"
        + @"|sans\s+tenir\s+compte(?:\s+de)?)"
        + @"\s+(?:l[ae]|ta|ton|tes|ce|cette|toute|la\s+moindre)?\s*"
        + @"(?:douleurs?|souffrances?|blessures?|genes?|inconforts?|signaux?|signal)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Mots-outils français, cherchés dans un texte d'au moins une phrase. Tous les autres
    /// garde-fous sont des lexiques français : une réponse en anglais les franchit intégralement
    /// — « You failed yesterday, hunter. Push through the pain. » est à la fois une
    /// culpabilisation et une injonction à ignorer la douleur — et s'afficherait telle quelle au
    /// Chasseur, en violation de la règle n°7 par-dessus le marché.
    ///
    /// <para>Une description d'une à deux phrases en porte forcément au moins un ; c'est ce qui
    /// rend l'heuristique fiable là où elle ne le serait pas sur un libellé nominal (« Éveil du
    /// Corps », « Ascension »).</para>
    /// </summary>
    private static readonly Regex MotsOutilsFrancais = new(
        @"\b(?:le|la|les|un|une|des|du|de|au|aux|en|et|ou|ton|ta|tes|son|sa|ses|mon|ma|mes"
        + @"|ce|cet|cette|qui|que|pour|par|avec|sans|sous|dans|sur|vers|chez|entre|contre"
        + @"|selon|pendant|avant|apr[eè]s|jusqu|aujourd|hui|ne|pas|plus|tu|toi|te|se|si|puis"
        + @"|alors|donc|mais|comme|encore|bien|tout|toute|tous|toutes|chaque|est|sont)\b|à",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Le pendant du contrôle précédent pour un libellé trop court pour porter un mot-outil : ici
    /// on ne cherche pas du français, on refuse ce qui est visiblement anglais.
    /// </summary>
    private static readonly Regex MarqueurAnglais = new(
        @"\b(?:you|your|the|and|with|through|push|keep|today|yesterday|dont|don't|reps"
        + @"|workout|hunter|no\s+excuses)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Débarrasse le texte de ses accents pour que la recherche de vocabulaire interdit ne se
    /// laisse pas contourner par un mot mal accentué, et ramène l'apostrophe typographique à
    /// l'apostrophe droite — un modèle emploie volontiers la première, et « tu n’as pas »
    /// passerait alors au travers de « tu n'as pas ».
    /// </summary>
    public static string SansAccents(string texte) =>
        new(texte
            .Replace('’', '\'')
            .Replace('ʼ', '\'')
            .Normalize(NormalizationForm.FormD)
            .Where(caractere =>
                CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
            .ToArray());

    /// <summary>S'applique au texte brut, accents compris.</summary>
    public static bool EstVisiblementAnglais(string texte) => MarqueurAnglais.IsMatch(texte);

    /// <summary>
    /// S'applique au texte brut, accents compris. Fiable seulement sur un texte d'au moins une
    /// phrase — voir <see cref="MotsOutilsFrancais"/>.
    /// </summary>
    public static bool SembleEtreEnFrancais(string texte) => MotsOutilsFrancais.IsMatch(texte);

    public static bool PrescritDesChiffres(string normalise) =>
        PrescriptionChiffree.IsMatch(normalise) || UniteDePrescription.IsMatch(normalise);

    public static bool InviteAPasserOutreLaDouleur(string normalise) =>
        InjonctionAPasserOutre.IsMatch(normalise);

    public static bool Reproche(string normalise) => ReprocheImplicite.IsMatch(normalise);

    public static bool EmploieUnVocabulaireInterdit(string normalise) =>
        VocabulaireInterdit.IsMatch(normalise);
}
