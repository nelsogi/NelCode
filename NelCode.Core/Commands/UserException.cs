namespace NelCode.Commands;

/// <summary>Une erreur de l'utilisateur (commande mal tapée, modèle inconnu...) : affichée sans le nom de l'exception.</summary>
public sealed class UserException(string message) : Exception(message);
