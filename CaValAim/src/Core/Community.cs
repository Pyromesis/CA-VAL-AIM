using Godot;

namespace CaValAim.Core;

/// <summary>
/// The community pages the routines and the glossary link to. Every address is compiled in and checked against this
/// allowlist before it reaches the browser: the screens never open a URL that came from a file or the network.
/// </summary>
public static class CommunityLinks
{
    public const string VoltaicDiscord = "https://discord.gg/voltaic";
    public const string VoltaicSite = "https://voltaic.gg/";
    public const string VoltaicTwitter = "https://twitter.com/voltaicaim";
    public const string VoltaicPatreon = "https://www.patreon.com/voltaicaim";
    public const string VoltaicSini = "https://twitter.com/sinizap";
    public const string AimJourney = "https://docs.google.com/document/d/1JoNtoHK9GgJCjE-7yQxKXkpAkGJyOBBipiZqPNYwECs/edit";
    public const string AimGlossary = "https://docs.google.com/document/d/1evzPXTABYsNg3udHhl12ZkfBogscKhsEdF7AONjZPso/edit";
    public const string AimlabsRoutines = "https://docs.google.com/document/d/1G57JXXqyrF8Tz_keWhgPDeCwSp-dcjHmlVBUFWgskHs/edit";
    public const string KvKsRoutines = "https://docs.google.com/document/d/1TpFHOg6WbPS2iFie2z53AnyQTXQ_ZBg7lKNqjvWhXIE/edit";
    public const string CartoonGuide = "https://docs.google.com/document/d/1EXOctwlIy1-JwTH-diMplmQwlUv2zbUoIisr63GliQg/edit";
    public const string CartoonTwitch = "https://www.twitch.tv/cartoon";
    public const string CartoonTwitter = "https://twitter.com/cartoonFPS";
    public const string CartoonYouTube = "https://www.youtube.com/c/cartoonfps";
    public const string Minigodwarmup = "https://docs.google.com/document/d/1XGqfG1JQXqzuRNR7AGAXI7nxIbrm81BXS03zG1RTbIw/edit";
    public const string Minigod = "https://twitter.com/minigodcs";

    static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        VoltaicDiscord, VoltaicSite, VoltaicTwitter, VoltaicPatreon, VoltaicSini, AimJourney, AimGlossary,
        AimlabsRoutines, KvKsRoutines, CartoonGuide, CartoonTwitch, CartoonTwitter, CartoonYouTube,
        Minigodwarmup, Minigod,
    };

    /// <summary>Opens one of the known community pages; returns false for anything else (and never throws).</summary>
    public static bool Open(string? url)
    {
        if (url == null || !Allowed.Contains(url)) return false;
        try { return OS.ShellOpen(url) == Error.Ok; }
        catch (Exception e) { Log.Error($"Couldn't open {url}: {e.Message}"); return false; }
    }

    /// <summary>Human-readable host for the "source" lines, e.g. "docs.google.com".</summary>
    public static string Host(string url)
    {
        try { return new Uri(url).Host; } catch { return url; }
    }
}
