using Arise.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Arise.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Username)
            .IsRequired()
            // Le même plafond que le validator : une colonne plus étroite tronquerait un
            // nom que l'inscription vient d'accepter.
            .HasMaxLength(User.LongueurMaximaleNom)
            // Le nom sert d'identifiant de connexion : « Sung » et « sung » doivent désigner
            // le même Chasseur, à l'index unique comme à la recherche.
            .UseCollation(AriseDbContext.CollationInsensibleALaCasse);

        // Le handler d'inscription vérifie l'unicité par une lecture préalable, ce qui ne
        // tranche pas une course entre deux inscriptions simultanées. C'est cet index qui la
        // tranche — la lecture ne sert qu'à formuler un message en français plutôt que de
        // laisser remonter une violation de contrainte.
        builder.HasIndex(user => user.Username).IsUnique();

        // Pas de plafond sur l'empreinte : sa longueur dépend de l'algorithme, et le jour
        // d'une migration d'algorithme, une colonne trop étroite la tronquerait
        // silencieusement — c'est-à-dire enfermerait les Chasseurs dehors.
        builder.Property(user => user.PasswordHash)
            .IsRequired();

        builder.Property(user => user.RegisteredAt)
            .IsRequired();
    }
}
