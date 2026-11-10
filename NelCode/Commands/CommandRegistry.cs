using System.Text.RegularExpressions;

namespace NelCode.Commands;

/// <summary>Toutes les classes ICommand du programme, créées une fois au démarrage.</summary>
public sealed partial class CommandRegistry
{
    private readonly Dictionary<string, ICommand> _commands;

    public CommandRegistry()
    {
        _commands = typeof(ICommand).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(ICommand).IsAssignableFrom(type))
            .Select(type => (ICommand)Activator.CreateInstance(type)!)
            .ToDictionary(command => command.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Par ordre alphabétique.</summary>
    public IReadOnlyList<ICommand> Commands => _commands.Values.OrderBy(c => c.Name, StringComparer.Ordinal).ToList();

    /// <summary>Exécute une ligne qui commence par "/", ex. "/import "C:\Mes modèles\v1" v1".</summary>
    public void Execute(CommandContext context, string line)
    {
        string[] words = Words().Matches(line.TrimStart('/'))
            .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Value)
            .ToArray();
        string name = words.Length > 0 ? words[0] : "";

        if (!_commands.TryGetValue(name, out var command))
        {
            var close = Commands.Where(c => name.Length > 0 && c.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                .Select(c => "/" + c.Name).ToList();
            throw new UserException($"Commande inconnue : /{name}." +
                                    (close.Count > 0 ? $" Tu voulais dire {string.Join(" ou ", close)} ?" : " /help pour la liste."));
        }

        command.Execute(context, words[1..]);
    }

    // Un mot = "texte entre guillemets" ou une suite sans espace.
    [GeneratedRegex(@"""([^""]*)""|\S+")]
    private static partial Regex Words();
}
