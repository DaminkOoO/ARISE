using Arise.Application.Features.Auth.Commands.RegisterUser;
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

        RuleFor(commande => commande.Username)
            .NotEmpty()
                .WithMessage("Le nom de Chasseur est obligatoire.");

        // Vérifier une empreinte coûte autant que la calculer : la route de connexion étant
        // ouverte sans authentification, le plafond de l'inscription vaut ici aussi. Il est
        // repris de RegisterUserCommandValidator plutôt que redéclaré — deux plafonds qui
        // divergent laisseraient un mot de passe inscriptible mais plus utilisable.
        RuleFor(commande => commande.Password)
            .NotEmpty()
                .WithMessage("Le mot de passe est obligatoire.")
            .MaximumLength(RegisterUserCommandValidator.LongueurMaximaleMotDePasse)
                .WithMessage(
                    "Le mot de passe ne peut pas dépasser "
                    + $"{RegisterUserCommandValidator.LongueurMaximaleMotDePasse} caractères.");
    }
}
