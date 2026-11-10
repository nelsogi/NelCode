using System.Text;
using NelCode.Commands;
using NelCode.Storage;
using NelCode.UI;
using Spectre.Console;
using TorchSharp;
using static TorchSharp.torch;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;
Console.CancelKeyPress += (_, _) => AnsiConsole.Cursor.Show();   // Spectre a pu cacher le curseur

// Dossier de travail : là où on lance Nel Code, ou --dir <dossier>.
string workingDirectory = Directory.GetCurrentDirectory();
int dirIndex = Array.IndexOf(args, "--dir");
if (dirIndex >= 0)
{
    if (dirIndex + 1 >= args.Length)
    {
        Messages.Error("--dir attend un dossier, par exemple : --dir C:\\MesProjets");
        return 1;
    }
    workingDirectory = Path.GetFullPath(args[dirIndex + 1]);
    if (!Directory.Exists(workingDirectory))
    {
        Messages.Error($"Dossier introuvable : {workingDirectory}");
        return 1;
    }
}

var settings = Settings.Load(out string? warning);
if (warning is not null)
    Messages.Warning(warning);

// GPU NVIDIA s'il y en a un d'utilisable, sinon CPU.
var device = cuda.is_available() ? CUDA : CPU;
var context = new CommandContext(new CommandRegistry(), new ConsoleOutput(), settings, device, workingDirectory);
context.LoadDefaultModel();

new Shell(context).Run();
return 0;
