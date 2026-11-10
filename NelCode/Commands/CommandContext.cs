using System.Globalization;
using NelCode.Engine;
using NelCode.Storage;
using TorchSharp;
using static TorchSharp.torch;

namespace NelCode.Commands;

/// <summary>
/// L'état de Nel Code, que les commandes lisent et changent : le modèle actif, la conversation en cours,
/// les réglages, le dossier de travail.
/// </summary>
public sealed class CommandContext
{
    public CommandContext(CommandRegistry registry, Settings settings, Device device, string workingDirectory)
    {
        Registry = registry;
        Settings = settings;
        Device = device;
        WorkingDirectory = workingDirectory;
        Session = NewSessionObject();
    }

    public CommandRegistry Registry { get; }
    public Settings Settings { get; }
    public Device Device { get; }
    public bool OnGpu => Device.type == DeviceType.CUDA;
    public string WorkingDirectory { get; }

    /// <summary>Le nom du modèle actif, null si aucun.</summary>
    public string? ModelName { get; private set; }

    /// <summary>Le chat avec le modèle actif, null si aucun.</summary>
    public ChatEngine? Chat { get; private set; }

    /// <summary>La conversation en cours. Écrite sur le disque dès sa première réponse.</summary>
    public Session Session { get; private set; }

    public bool QuitRequested { get; set; }

    public ChatEngine RequireChat() =>
        Chat ?? throw new UserException("Aucun modèle actif : /import <fichier.nelmodel> pour en installer un, /models pour la liste.");

    /// <summary>Charge un modèle installé, qui devient le modèle retenu pour le prochain lancement.</summary>
    public void LoadModel(string name)
    {
        if (!ModelLibrary.Exists(name))
            throw new UserException($"Modèle inconnu : « {name} ». /models pour la liste.");

        var generator = ModelLoader.Load(ModelLibrary.Folder(name), Device);
        Chat?.Generator.Dispose();
        Chat = new ChatEngine(generator);
        ModelName = name;
        Session.Model = name;

        Settings.LastModel = name;
        Settings.Save();
    }

    /// <summary>Plus de modèle actif (avant de supprimer le modèle chargé). La conversation reste.</summary>
    public void UnloadModel()
    {
        Chat?.Generator.Dispose();
        Chat = null;
        if (Settings.LastModel == ModelName)
        {
            Settings.LastModel = null;
            Settings.Save();
        }
        ModelName = null;
    }

    /// <summary>Une nouvelle conversation vide (la précédente reste sur le disque).</summary>
    public void NewSession() => Session = NewSessionObject();

    /// <summary>Reprend une conversation sauvegardée (et son modèle, s'il est encore installé).</summary>
    public void ResumeSession(Session session)
    {
        if (session.Model != ModelName && ModelLibrary.Exists(session.Model))
            LoadModel(session.Model);
        Session = session;
        Session.Model = ModelName ?? session.Model;
    }

    /// <summary>Ajoute une question et sa réponse à la conversation, et la sauvegarde.</summary>
    public void RecordTurn(string user, string reply)
    {
        Session.AddTurn(user, reply);
        SessionStore.Save(Session);
    }

    private Session NewSessionObject() => new() { Model = ModelName ?? "", WorkingDirectory = WorkingDirectory };

    /// <summary>Un nombre tapé par l'utilisateur : "0,8" ou "0.8".</summary>
    public static double ParseNumber(string text, string usage) =>
        double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : throw new UserException($"« {text} » n'est pas un nombre. Utilisation : {usage}");
}
