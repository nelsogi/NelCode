# Nel Code

Application pour discuter avec les modèles entraînés par **MiniChat**, dans le style de Claude Code : en
console, ou dans une fenêtre Windows au style terminal. Nel Code n'entraîne rien : il charge un dossier de
modèle MiniChat et génère les réponses.

## Projets

| Projet | Rôle |
|---|---|
| `NelCode.Core` | bibliothèque : moteur (TorchSharp), modèles, conversations, réglages, commandes. Ni Spectre.Console ni WinForms |
| `NelCode.Console` | l'interface console (Spectre.Console), exécutable `NelCode.exe` |
| `NelCode.Desktop` | l'interface WinForms (net10.0-windows, x64) |

Les commandes n'écrivent jamais directement : elles passent par `IOutput` (`NelCode.Core/Commands/IOutput.cs`),
que chaque interface implémente (`ConsoleOutput`, `DesktopOutput`).

## Lancer

```powershell
dotnet run --project NelCode.Console                     # dossier de travail = dossier courant
dotnet run --project NelCode.Console -- --dir C:\Projets # autre dossier de travail
dotnet run --project NelCode.Desktop                     # la fenêtre (--dir marche aussi)
```

Le GPU NVIDIA (CUDA) est utilisé s'il est disponible, sinon le CPU. L'écran d'accueil affiche le modèle actif,
GPU ou CPU, et le dossier de travail.

## Desktop

- À gauche : les conversations groupées par date (clic pour reprendre), les modèles (clic pour changer),
  le bouton Nouvelle conversation.
- Au centre : la conversation ; en haut, le modèle, GPU/CPU et le dossier de travail (clic pour le changer).
- En bas : la saisie, avec les mêmes touches que la console (suggestions après `/`, historique ↑↓),
  plus `Maj+Entrée` pour aller à la ligne. `Échap` arrête une réponse.
- Barre d'état : modèle, GPU/CPU, tokens/s, température.
- Glisser un `.nelmodel` sur la fenêtre l'installe (comme `/import`).

Toutes les commandes marchent aussi ; leurs questions (confirmer, choisir) s'ouvrent dans une petite fenêtre.

## Installer un modèle

Normalement à partir d'un fichier `.nelmodel` exporté par MiniChat (format décrit dans
[NELMODEL-FORMAT.md](NELMODEL-FORMAT.md)) :

```
/import "C:\...\minichat-v2.nelmodel"
```

Nel Code vérifie `formatVersion` et la présence des quatre fichiers, affiche les infos de `model-info.json`,
puis teste le chargement avant d'ajouter le modèle à la liste. Le nom vient de `[nom]`, sinon de `name` dans
`model-info.json`, sinon du nom du fichier. Si `parameters` ne correspond pas au modèle chargé, un
avertissement s'affiche.

Si le nom existe déjà, Nel Code propose de remplacer, de choisir un autre nom ou d'annuler. `--replace`
remplace sans demander. L'ancien modèle n'est supprimé qu'une fois le nouveau vérifié.

Pour les tests, un dossier de modèle MiniChat (`config.json`, `tokenizer.json`, `weights.dat`) marche aussi,
par exemple `MiniChat\bin\Debug\net10.0\model` :

```
/import "C:\...\MiniChat\bin\Debug\net10.0\model" minichat-v1
```

Dans tous les cas, `optimizer.dat` n'est pas copié et un modèle qui ne se charge pas n'est pas installé.

## Saisie

| Touche | Effet |
|---|---|
| `/` | ouvre les suggestions de commandes |
| `Tab` | complète la commande choisie |
| `↑` `↓` | choisissent une suggestion, sinon parcourent l'historique |
| `Entrée` | envoie le message ou lance la commande |
| `Échap` | ferme les suggestions / vide la ligne ; **pendant une réponse, l'arrête** |
| `Ctrl+D` | sur une ligne vide, quitte |

## Commandes

| Commande | Effet |
|---|---|
| `/import <fichier.nelmodel \| dossier> [nom] [--replace]` | installe un modèle (voir plus haut) |
| `/delete <nom>` | supprime un modèle, après confirmation |
| `/models` | liste les modèles : nom, taille, date, forme |
| `/model <nom>` | change de modèle (retenu au prochain lancement), nouvelle conversation |
| `/new` | nouvelle conversation |
| `/resume` | liste les conversations sauvegardées pour en reprendre une |
| `/temp [0.8]` | température (0.1 à 2) |
| `/topk [20]` | tirage parmi les K tokens les plus probables (0 = pas de limite) |
| `/topp [0.9]` | tirage parmi les tokens qui font cette part des chances (1 = pas de limite) |
| `/info` | modèle, GPU/CPU, réglages, conversation, dossiers |
| `/clear` | efface l'écran (la conversation continue) |
| `/help` | liste des commandes |
| `/quit` | quitte |

Un chemin avec des espaces s'écrit entre guillemets.

## Fichiers

Tout est dans `%APPDATA%\NelCode` :

```
models\<nom>\           config.json, tokenizer.json, weights.dat (+ model-info.json ou training.json)
sessions\<id>.json      une conversation : modèle, dossier de travail, date, messages
settings.json           température, top-k, top-p, dernier modèle utilisé
```

Une conversation est sauvegardée après chaque réponse (une réponse arrêtée par Échap est gardée telle quelle).

## Code

```
NelCode/
  Program.cs      options (--dir), GPU/CPU, chargement du dernier modèle, lancement du Shell
  Engine/         copie de ce qu'il faut de MiniChat pour générer (rien pour l'entraînement)
    ModelConfig, MiniGpt, TransformerBlock, CausalSelfAttention, Rope, RmsNorm, SwiGlu, FeedForward, KvCache
    BpeTokenizer, ChatFormat, TextGenerator (température, top-k, top-p, arrêt sur <|end|>)
    ModelLoader (lit un dossier), ChatEngine (construit le prompt avec les derniers tours qui tiennent)
  Storage/        AppPaths, Settings, ModelLibrary (/import, /models), NelModelPackage (.nelmodel), Session, SessionStore
  Commands/       ICommand, une classe par commande, CommandRegistry (trouve les ICommand tout seul), CommandContext (état)
  UI/             Shell (la boucle), InputBox (saisie), Prompts (questions o/n, choix), ChatView, WelcomeBanner, EscapeKey, Messages, Theme
```

Ajouter une commande : une classe qui implémente `ICommand` dans `Commands/`, rien d'autre à déclarer.

**Compatibilité avec MiniChat.** Les champs des modules (`_tokenEmbedding`, `_blocks`, `_qkv`, `_gate`, `weight`…)
ont exactement les mêmes noms que dans MiniChat : ce sont les clés des poids dans `weights.dat`. Les renommer
empêche le chargement. Si MiniChat change son architecture, il faut reporter le changement dans `Engine/`.
Les deux architectures sont gérées : Modern (RoPE, RMSNorm, SwiGLU) et Classic (comme GPT-2).
La phrase d'identité (`ChatFormat.IdentityText`) est celle vue à l'entraînement : à garder telle quelle.

Paquets : `Spectre.Console` 0.49.1 et `TorchSharp-cuda-windows` 0.105.* (CPU seul sous Linux), comme MiniChat.
Le `.csproj` reprend de MiniChat le recollage de `cusparse64_12.dll` dans le cache NuGet, sans lequel le GPU
n'est pas détecté.
