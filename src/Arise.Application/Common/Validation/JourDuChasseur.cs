namespace Arise.Application.Common.Validation;

/// <summary>
/// Le jour tel que le Chasseur le vit, et la fenêtre d'instants UTC qu'il recouvre.
///
/// <para>Toute la gamification est datée sur ce jour-là et jamais sur celui du serveur (doc
/// mécaniques, section 2) : la séance du 25 validée à 00h05 le 26 appartient au 25.</para>
/// </summary>
public static class JourDuChasseur
{
    /// <summary>
    /// La date d'aujourd'hui dans le fuseau du Chasseur.
    /// </summary>
    /// <exception cref="TimeZoneNotFoundException">
    /// Le fuseau est inconnu — cas que le validator de la commande écarte en amont, avec un
    /// message français.
    /// </exception>
    public static DateOnly Aujourdhui(TimeProvider horloge, string fuseauHoraire) =>
        DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(
                horloge.GetUtcNow(),
                TimeZoneInfo.FindSystemTimeZoneById(fuseauHoraire))
                .DateTime);

    /// <summary>
    /// Les instants UTC que ce jour recouvre chez le Chasseur : <c>[Debut, Fin[</c>.
    ///
    /// <para>Sert à interroger des colonnes horodatées en UTC — « les tâches cochées
    /// aujourd'hui » — sans faire traverser le fuseau jusqu'au stockage.</para>
    ///
    /// <para>Le décalage est lu <b>à chaque borne</b> plutôt qu'une fois pour la journée : un
    /// jour de changement d'heure n'a pas le même décalage à minuit et à minuit le lendemain, et
    /// une fenêtre calculée sur un seul décalage manquerait ou compterait deux fois une heure.
    /// </para>
    /// </summary>
    public static (DateTimeOffset Debut, DateTimeOffset Fin) FenetreUtc(
        DateOnly jour, string fuseauHoraire)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(fuseauHoraire);

        return (MinuitUtc(jour, zone), MinuitUtc(jour.AddDays(1), zone));
    }

    private static DateTimeOffset MinuitUtc(DateOnly jour, TimeZoneInfo zone)
    {
        var minuitLocal = jour.ToDateTime(TimeOnly.MinValue);

        return new DateTimeOffset(minuitLocal, zone.GetUtcOffset(minuitLocal)).ToUniversalTime();
    }
}
