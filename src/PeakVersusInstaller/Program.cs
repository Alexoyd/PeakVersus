using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace PeakVersusInstaller;

// Installs BepInEx and PEAK Versus straight into the game folder, so friends only double-click one exe.
internal static class Program
{
    const string BepInExPackUrl = "https://thunderstore.io/package/download/BepInEx/BepInExPack_PEAK/5.4.2403/";
    const string ModFolder = "PeakVersus";
    const string ModFile = "com.github.Alexoyd.PeakVersus.dll";

    static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Title = "PEAK Versus — installation";
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        Console.WriteLine("=== PEAK Versus ===");
        Console.WriteLine();
        try
        {
            string? game = FindGame();
            if (game == null)
            {
                Console.WriteLine("PEAK introuvable automatiquement.");
                Console.Write("Colle le chemin du dossier de PEAK (celui qui contient PEAK.exe) : ");
                game = Console.ReadLine()?.Trim().Trim('"');
                if (string.IsNullOrEmpty(game) || !File.Exists(Path.Combine(game, "PEAK.exe")))
                {
                    return Fail("PEAK.exe introuvable dans ce dossier.");
                }
            }
            Console.WriteLine($"PEAK trouvé : {game}");
            Console.WriteLine();
            Console.WriteLine("  1. Installer ou mettre à jour PEAK Versus");
            Console.WriteLine("  2. Jouer sans mods (désactiver)");
            Console.WriteLine("  3. Réactiver les mods");
            Console.WriteLine("  4. Désinstaller PEAK Versus");
            Console.Write("Choix [1] : ");
            string choice = Console.ReadLine()?.Trim() ?? "";
            switch (choice)
            {
                case "2": Toggle(game, enable: false); break;
                case "3": Toggle(game, enable: true); break;
                case "4": Uninstall(game); break;
                default: Install(game); break;
            }
            return Done();
        }
        catch (Exception e)
        {
            return Fail(e.Message);
        }
    }

    static void Install(string game)
    {
        if (System.Diagnostics.Process.GetProcessesByName("PEAK").Length > 0)
        {
            throw new InvalidOperationException("PEAK est lancé : ferme le jeu puis relance l'installation.");
        }
        string bepinex = Path.Combine(game, "BepInEx");
        if (!File.Exists(Path.Combine(bepinex, "core", "BepInEx.dll")))
        {
            Console.WriteLine("Téléchargement de BepInEx (chargeur de mods)…");
            string zip = Path.Combine(Path.GetTempPath(), "BepInExPack_PEAK.zip");
            using (WebClient client = new WebClient())
            {
                client.DownloadFile(BepInExPackUrl, zip);
            }
            ExtractFolder(zip, "BepInExPack_PEAK/", game);
            File.Delete(zip);
            Console.WriteLine("BepInEx installé.");
        }
        else
        {
            Console.WriteLine("BepInEx déjà présent.");
        }
        Toggle(game, enable: true, quiet: true);

        string target = Path.Combine(bepinex, "plugins", ModFolder);
        Directory.CreateDirectory(target);
        using (Stream? dll = typeof(Program).Assembly.GetManifestResourceStream("PeakVersus.dll"))
        {
            if (dll == null) { throw new InvalidOperationException("Le mod n'est pas inclus dans cet installeur."); }
            using FileStream file = File.Create(Path.Combine(target, ModFile));
            dll.CopyTo(file);
        }
        Console.WriteLine("PEAK Versus installé.");
        Console.WriteLine();
        Console.WriteLine("Lance PEAK depuis Steam, puis clique sur « Host PEAK Versus » (l'hôte) ou rejoins la partie d'un ami.");
    }

    static void ExtractFolder(string zipPath, string prefix, string destination)
    {
        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        string root = Path.GetFullPath(destination);
        foreach (ZipArchiveEntry entry in archive.Entries.Where(e => e.FullName.StartsWith(prefix)))
        {
            string relative = entry.FullName.Substring(prefix.Length);
            if (relative == "") { continue; }
            string path = Path.GetFullPath(Path.Combine(root, relative));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) { continue; }
            if (entry.FullName.EndsWith("/"))
            {
                Directory.CreateDirectory(path);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            entry.ExtractToFile(path, overwrite: true);
        }
    }

    // BepInEx loads through winhttp.dll: renaming it switches every mod off without deleting anything.
    static void Toggle(string game, bool enable, bool quiet = false)
    {
        string on = Path.Combine(game, "winhttp.dll");
        string off = Path.Combine(game, "winhttp.dll.off");
        if (enable && File.Exists(off) && !File.Exists(on)) { File.Move(off, on); }
        if (!enable && File.Exists(on)) { File.Move(on, off); }
        if (!quiet) { Console.WriteLine(enable ? "Mods réactivés." : "Mods désactivés : PEAK se lance normalement."); }
    }

    static void Uninstall(string game)
    {
        string target = Path.Combine(game, "BepInEx", "plugins", ModFolder);
        if (Directory.Exists(target)) { Directory.Delete(target, recursive: true); }
        Console.WriteLine("PEAK Versus désinstallé. BepInEx reste en place (option 2 pour jouer sans mods).");
    }

    static string? FindGame()
    {
        List<string> libraries = new();
        string? steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
        if (steam != null)
        {
            libraries.Add(steam.Replace('/', '\\'));
            string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                {
                    libraries.Add(match.Groups[1].Value.Replace("\\\\", "\\"));
                }
            }
        }
        libraries.Add(@"C:\Program Files (x86)\Steam");
        foreach (string library in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string game = Path.Combine(library, "steamapps", "common", "PEAK");
            if (File.Exists(Path.Combine(game, "PEAK.exe"))) { return game; }
        }
        return null;
    }

    static int Done()
    {
        Console.WriteLine();
        Console.WriteLine("Terminé. Appuie sur Entrée pour fermer.");
        Console.ReadLine();
        return 0;
    }

    static int Fail(string message)
    {
        Console.WriteLine();
        Console.WriteLine($"Erreur : {message}");
        Console.WriteLine("Appuie sur Entrée pour fermer.");
        Console.ReadLine();
        return 1;
    }
}
