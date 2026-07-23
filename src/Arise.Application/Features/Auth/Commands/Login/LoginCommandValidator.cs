using Arise.Domain.Users;
using FluentValidation;

namespace Arise.Application.Features.Auth.Commands.Login;

/// <summary>
/// Contrôles de forme de la connexion : les deux champs sont remplis, et le mot de passe
/// reste d'une taille raisonnable. Rien de plus.
///
/// <para>La connexion ne rejoue délibérément pas les règles de l'inscription — longueur
/// minimale, caractères autorisés. Elles ne protègent rien ici (le mot de passe est déjà
/// choisi), et refuser une tentative avant de l'avoir vérifiée enfermerait dehors tout
/// compte créé sous une politique antérieure.</para>
/// </summary>
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        // Le handler rogne le nom avant de chercher le compte : le même plafond qu'à
        // l'inscription s'impose donc ici, et pour la même raison — sans lui, un nom précédé
        // de 25 Mo d'espaces fait allouer autant, sur une route ouverte sans
        // authentification. Ce n'est pas rejouer la politique de nommage : c'est une borne
        // d'allocation, et elle vaut aussi pour un compte créé sous une politique
        // antérieure.
        RuleFor(commande => commande.Username)
            .NotEmpty()
                .WithMessage("Le nom de Chasseur est obligatoire.")
            .MaximumLength(PolitiqueIdentifiants.LongueurMaximaleNomBrut)
                .WithMessage(
                    "Le nom de Chasseur ne peut pas dépasser "
                    + $"{User.LongueurMaximaleNom} caractères.");

        // Vérifier une empreinte coûte autant que la calculer : la route de connexion étant
        // ouverte sans authentification, le plafond de l'inscription vaut ici aussi.
        RuleFor(commande => commande.Password)
            .NotEmpty()
                .WithMessage("Le mot de passe est obligatoire.")
            .MaximumLength(PolitiqueIdentifiants.LongueurMaximaleMotDePasse)
                .WithMessage(
                    "Le mot de passe ne peut pas dépasser "
                    + $"{PolitiqueIdentifiants.LongueurMaximaleMotDePasse} caractères.");
    }
}
