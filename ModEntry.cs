using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TheLivingValleyExpanded;

public sealed class ModEntry : Mod
{
    private const string TargetModId = "mx146323.StardewLivingRPG";
    private const string HarmonyId = "ai2claw.TheLivingValleyExpanded";
    private const string BaseLoreRelativePath = @"assets\sve-lore.json";
    private const string OverrideLoreRelativePath = @"assets\sve-lore.override.json";

    private static readonly Regex CanonNpcBlockRegex = new(
        @"CANON_NPCS:\s*\[(?<list>[^\]]*)\]",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] DefaultSveNpcNames =
    {
        "Magnus",
        "Andy",
        "Apples",
        "Alesia",
        "Camilla",
        "Claire",
        "Hank",
        "Isaac",
        "Jadu",
        "Jolyne",
        "Lance",
        "Marlon",
        "Martin",
        "Morgan",
        "Morris",
        "Olivia",
        "Peaches",
        "Scarlett",
        "Sophia",
        "Suki",
        "Susan",
        "Treyvon",
        "Victor"
    };

    private static readonly string[] IgnoredFriendshipNames =
    {
        "Pet",
        "Horse"
    };

    private static readonly Dictionary<string, string> LoreNpcAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["wizard"] = "magnus",
        ["rasmodius"] = "magnus"
    };

    private static ModEntry? Instance;
    private ModConfig _config = new();
    private Harmony? _harmony;
    private bool _patched;
    private bool _loreLoaded;
    private string _activeLoreLocale = string.Empty;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly Dictionary<string, SveNpcLoreEntry> _baseNpcLoreByToken = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _baseLocationLoreByToken = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SveNpcLoreEntry> _npcLoreByToken = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _locationLoreByToken = new(StringComparer.OrdinalIgnoreCase);

    public override void Entry(IModHelper helper)
    {
        Instance = this;
        _config = helper.ReadConfig<ModConfig>();
        LoadLoreFile();

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        if (!Helper.ModRegistry.IsLoaded(TargetModId))
        {
            Monitor.Log($"Skipped patching because '{TargetModId}' is not installed.", LogLevel.Info);
            return;
        }

        TryPatchLivingRpg();
        InjectRumorBoardNpcTargets("GameLaunched");
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        InjectRumorBoardNpcTargets("SaveLoaded");
        RefreshLoreForCurrentLocale(force: true);
    }

    private void LoadLoreFile()
    {
        _baseNpcLoreByToken.Clear();
        _baseLocationLoreByToken.Clear();
        _npcLoreByToken.Clear();
        _locationLoreByToken.Clear();
        _loreLoaded = false;
        _activeLoreLocale = string.Empty;

        var lorePath = Path.Combine(Helper.DirectoryPath, "assets", "sve-lore.json");
        if (!File.Exists(lorePath))
        {
            Monitor.Log($"SVE lore file not found at {BaseLoreRelativePath}. Lore injection disabled.", LogLevel.Trace);
            return;
        }

        var parsed = TryReadLoreFile(lorePath, "base lore");
        if (parsed is null)
            return;

        foreach (var (rawName, entry) in parsed.Npcs)
        {
            if (entry is null)
                continue;

            var token = ResolveLoreNpcToken(rawName);
            if (string.IsNullOrWhiteSpace(token))
                continue;

            _baseNpcLoreByToken[token] = CloneLoreEntry(entry);
        }

        foreach (var (rawLocation, text) in parsed.Locations)
        {
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var token = NormalizeLocationToken(rawLocation);
            if (string.IsNullOrWhiteSpace(token))
                continue;

            _baseLocationLoreByToken[token] = text.Trim();
        }

        _loreLoaded = _baseNpcLoreByToken.Count > 0 || _baseLocationLoreByToken.Count > 0;
        if (!_loreLoaded)
        {
            Monitor.Log("SVE base lore is empty. Lore injection disabled.", LogLevel.Warn);
            return;
        }

        Monitor.Log(
            $"Loaded base SVE lore: npc={_baseNpcLoreByToken.Count}, locations={_baseLocationLoreByToken.Count}.",
            LogLevel.Info);

        RefreshLoreForCurrentLocale(force: true);
    }

    private SveLoreFile? TryReadLoreFile(string filePath, string description)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var parsed = JsonSerializer.Deserialize<SveLoreFile>(json, _jsonOptions);
            if (parsed is null)
            {
                Monitor.Log($"SVE {description} file is empty or invalid JSON object: {filePath}", LogLevel.Warn);
                return null;
            }

            return parsed;
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed to load SVE {description} file '{filePath}': {ex.Message}", LogLevel.Warn);
            return null;
        }
    }

    private void RefreshLoreForCurrentLocale(bool force = false)
    {
        if (!_loreLoaded)
            return;

        var locale = ResolveActiveLoreLocale();
        if (!force && string.Equals(locale, _activeLoreLocale, StringComparison.OrdinalIgnoreCase))
            return;

        _activeLoreLocale = locale;
        _npcLoreByToken.Clear();
        _locationLoreByToken.Clear();

        foreach (var (token, entry) in _baseNpcLoreByToken)
            _npcLoreByToken[token] = CloneLoreEntry(entry);

        foreach (var (token, text) in _baseLocationLoreByToken)
            _locationLoreByToken[token] = text;

        var applied = new List<string>();
        foreach (var candidate in BuildLocaleFallbackChain(locale))
        {
            var path = Path.Combine(Helper.DirectoryPath, "i18n", $"sve-lore.{candidate}.json");
            if (!File.Exists(path))
                continue;

            var overlay = TryReadLoreFile(path, $"locale overlay ({candidate})");
            if (overlay is null)
                continue;

            MergeLoreOverlay(overlay);
            applied.Add(candidate);
        }

        var overridePath = Path.Combine(Helper.DirectoryPath, "assets", "sve-lore.override.json");
        if (File.Exists(overridePath))
        {
            var overrideLore = TryReadLoreFile(overridePath, "local override");
            if (overrideLore is not null)
            {
                MergeLoreOverlay(overrideLore);
                applied.Add(OverrideLoreRelativePath);
            }
        }

        if (applied.Count > 0)
        {
            Monitor.Log(
                $"Applied SVE lore overlays for '{locale}': {string.Join(", ", applied)}",
                LogLevel.Info);
        }
    }

    private void MergeLoreOverlay(SveLoreFile overlay)
    {
        foreach (var (rawName, entry) in overlay.Npcs)
        {
            if (entry is null)
                continue;

            var token = ResolveLoreNpcToken(rawName);
            if (string.IsNullOrWhiteSpace(token))
                continue;

            if (!_npcLoreByToken.TryGetValue(token, out var existing))
            {
                existing = new SveNpcLoreEntry();
                _npcLoreByToken[token] = existing;
            }

            MergeLoreEntry(existing, entry);
        }

        foreach (var (rawLocation, text) in overlay.Locations)
        {
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var token = NormalizeLocationToken(rawLocation);
            if (string.IsNullOrWhiteSpace(token))
                continue;

            _locationLoreByToken[token] = text.Trim();
        }
    }

    private string ResolveActiveLoreLocale()
    {
        var overrideLocale = (_config.LoreLocaleOverride ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(overrideLocale))
            return NormalizeLocaleCode(overrideLocale);

        try
        {
            var localeProp = Helper.Translation.GetType().GetProperty("Locale", BindingFlags.Public | BindingFlags.Instance);
            var locale = localeProp?.GetValue(Helper.Translation)?.ToString();
            return NormalizeLocaleCode(locale);
        }
        catch
        {
            return "en";
        }
    }

    private static string NormalizeLocaleCode(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            return "en";

        return locale.Trim().Replace('_', '-').ToLowerInvariant();
    }

    private static IEnumerable<string> BuildLocaleFallbackChain(string locale)
    {
        var normalized = NormalizeLocaleCode(locale);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (seen.Add(normalized))
            yield return normalized;

        var dash = normalized.IndexOf('-', StringComparison.Ordinal);
        if (dash > 0)
        {
            var language = normalized[..dash];
            if (seen.Add(language))
                yield return language;
        }
    }

    private static SveNpcLoreEntry CloneLoreEntry(SveNpcLoreEntry source)
    {
        return new SveNpcLoreEntry
        {
            Role = source.Role ?? string.Empty,
            Persona = source.Persona ?? string.Empty,
            Speech = source.Speech ?? string.Empty,
            Ties = source.Ties ?? string.Empty,
            Boundaries = source.Boundaries ?? string.Empty
        };
    }

    private static void MergeLoreEntry(SveNpcLoreEntry target, SveNpcLoreEntry overlay)
    {
        if (!string.IsNullOrWhiteSpace(overlay.Role))
            target.Role = overlay.Role.Trim();
        if (!string.IsNullOrWhiteSpace(overlay.Persona))
            target.Persona = overlay.Persona.Trim();
        if (!string.IsNullOrWhiteSpace(overlay.Speech))
            target.Speech = overlay.Speech.Trim();
        if (!string.IsNullOrWhiteSpace(overlay.Ties))
            target.Ties = overlay.Ties.Trim();
        if (!string.IsNullOrWhiteSpace(overlay.Boundaries))
            target.Boundaries = overlay.Boundaries.Trim();
    }

    private string BuildSveLorePromptBlock(string? npcName)
    {
        if (!_config.EnableSveLoreInjection || !_loreLoaded || !IsSVEInstalled())
            return string.Empty;

        RefreshLoreForCurrentLocale();

        var parts = new List<string>();
        var npcLore = TryGetNpcLore(npcName, out var resolvedNpcName);
        var locationLore = TryGetLocationLore(Game1.currentLocation?.Name);

        if (npcLore is null && string.IsNullOrWhiteSpace(locationLore))
            return string.Empty;

        parts.Add("SVE_ROLEPLAY_RULE: Follow SVE_NPC_LORE and SVE_LOCATION_LORE exactly when provided.");

        if (npcLore is not null)
        {
            parts.Add(
                $"SVE_NPC_LORE[{resolvedNpcName}]: role={TrimForPrompt(npcLore.Role, 120)}; " +
                $"persona={TrimForPrompt(npcLore.Persona, 140)}; " +
                $"speech={TrimForPrompt(npcLore.Speech, 120)}; " +
                $"ties={TrimForPrompt(npcLore.Ties, 140)}; " +
                $"boundaries={TrimForPrompt(npcLore.Boundaries, 140)}.");
        }

        if (!string.IsNullOrWhiteSpace(locationLore))
            parts.Add($"SVE_LOCATION_LORE: {TrimForPrompt(locationLore, 220)}.");

        return string.Join(" ", parts);
    }

    private SveNpcLoreEntry? TryGetNpcLore(string? npcName, out string resolvedNpcName)
    {
        resolvedNpcName = (npcName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(npcName))
            return null;

        var token = ResolveLoreNpcToken(npcName);
        if (string.IsNullOrWhiteSpace(token))
            return null;

        if (_npcLoreByToken.TryGetValue(token, out var direct))
        {
            if (string.IsNullOrWhiteSpace(resolvedNpcName))
                resolvedNpcName = PrettyToken(token);
            return direct;
        }

        return null;
    }

    private string TryGetLocationLore(string? locationName)
    {
        if (string.IsNullOrWhiteSpace(locationName))
            return string.Empty;

        var token = NormalizeLocationToken(locationName);
        if (string.IsNullOrWhiteSpace(token))
            return string.Empty;

        if (_locationLoreByToken.TryGetValue(token, out var exact))
            return exact;

        foreach (var (key, value) in _locationLoreByToken.OrderByDescending(kv => kv.Key.Length))
        {
            if (token.Contains(key, StringComparison.OrdinalIgnoreCase)
                || key.Contains(token, StringComparison.OrdinalIgnoreCase))
                return value;
        }

        return string.Empty;
    }

    private static string ResolveLoreNpcToken(string rawName)
    {
        var normalized = NormalizeNpcToken(rawName);
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        if (LoreNpcAliases.TryGetValue(normalized, out var mapped))
            return mapped;

        return normalized;
    }

    private static string NormalizeLocationToken(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        return Regex.Replace(
            raw.Trim().ToLowerInvariant().Replace(" ", "_", StringComparison.Ordinal),
            @"[^a-z0-9_]+",
            string.Empty,
            RegexOptions.CultureInvariant);
    }

    private static string PrettyToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return string.Empty;

        var parts = token
            .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => char.ToUpperInvariant(p[0]) + p[1..]);
        return string.Join(" ", parts);
    }

    private static string TrimForPrompt(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var compact = Regex.Replace(value.Trim(), @"\s+", " ", RegexOptions.CultureInvariant);
        if (compact.Length <= maxLength)
            return compact;

        return compact[..maxLength].TrimEnd();
    }

    private void TryPatchLivingRpg()
    {
        if (_patched)
            return;

        var modEntryType = AccessTools.TypeByName("StardewLivingRPG.ModEntry");
        if (modEntryType is null)
        {
            Monitor.Log("Could not find StardewLivingRPG.ModEntry. Compatibility hooks were not applied.", LogLevel.Warn);
            return;
        }

        _harmony = new Harmony(HarmonyId);

        PatchMethod(
            _harmony,
            modEntryType,
            "GetExpandedNpcRoster",
            postfixName: nameof(GetExpandedNpcRosterPostfix));

        PatchMethod(
            _harmony,
            modEntryType,
            "BuildCompactGameStateInfo",
            postfixName: nameof(BuildCompactGameStateInfoPostfix));

        PatchMethod(
            _harmony,
            modEntryType,
            "TryFindNpcTargetInText",
            prefixName: nameof(TryFindNpcTargetInTextPrefix));

        PatchMethod(
            _harmony,
            modEntryType,
            "ResolveNpcTargetFromTownEvent",
            postfixName: nameof(ResolveNpcTargetFromTownEventPostfix));

        _patched = true;
        Monitor.Log("Applied The Living Valley Expanded compatibility patches.", LogLevel.Info);
    }

    private void PatchMethod(Harmony harmony, Type targetType, string targetName, string? prefixName = null, string? postfixName = null)
    {
        var target = AccessTools.Method(targetType, targetName);
        if (target is null)
        {
            Monitor.Log($"Patch skipped: target method '{targetName}' was not found.", LogLevel.Trace);
            return;
        }

        HarmonyMethod? prefix = null;
        HarmonyMethod? postfix = null;

        if (!string.IsNullOrWhiteSpace(prefixName))
        {
            var prefixMethod = AccessTools.Method(typeof(ModEntry), prefixName);
            if (prefixMethod is not null)
                prefix = new HarmonyMethod(prefixMethod);
        }

        if (!string.IsNullOrWhiteSpace(postfixName))
        {
            var postfixMethod = AccessTools.Method(typeof(ModEntry), postfixName);
            if (postfixMethod is not null)
                postfix = new HarmonyMethod(postfixMethod);
        }

        harmony.Patch(target, prefix, postfix);
        Monitor.Log($"Patched {targetType.FullName}.{target.Name}", LogLevel.Trace);
    }

    private void InjectRumorBoardNpcTargets(string source)
    {
        if (!Helper.ModRegistry.IsLoaded(TargetModId))
            return;

        var rumorBoardType = AccessTools.TypeByName("StardewLivingRPG.Systems.RumorBoardService");
        if (rumorBoardType is null)
            return;

        var validNpcTargetsField = rumorBoardType.GetField("ValidNpcTargets", BindingFlags.NonPublic | BindingFlags.Static);
        if (validNpcTargetsField?.GetValue(null) is not HashSet<string> validTargets)
            return;

        var added = 0;
        foreach (var token in GetCompatibilityNpcTokens())
        {
            if (validTargets.Add(token))
                added++;
        }

        if (added > 0)
            Monitor.Log($"[{source}] Added {added} NPC targets to RumorBoardService social_visit validation.", LogLevel.Info);
    }

    private IReadOnlyList<string> GetCompatibilityNpcNames()
    {
        var merged = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var includeSveDefaults = IsSVEInstalled();

        if (includeSveDefaults)
            AddNames(merged, seen, DefaultSveNpcNames);

        AddNames(merged, seen, ParseCsv(_config.AdditionalNpcNamesCsv));

        if (includeSveDefaults && _config.IncludeFriendshipNpcsWhenSVEInstalled && Context.IsWorldReady && Game1.player is not null)
        {
            AddNames(
                merged,
                seen,
                Game1.player.friendshipData.Keys.Where(n => !IgnoredFriendshipNames.Contains(n, StringComparer.OrdinalIgnoreCase)));
        }

        return merged;
    }

    private IEnumerable<string> GetCompatibilityNpcTokens()
    {
        return GetCompatibilityNpcNames()
            .Select(NormalizeNpcToken)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ParseCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return Array.Empty<string>();

        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s));
    }

    private void AddNames(List<string> merged, HashSet<string> seen, IEnumerable<string> names)
    {
        foreach (var raw in names)
        {
            var name = (raw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (seen.Add(name))
                merged.Add(name);
        }
    }

    private bool IsSVEInstalled()
    {
        return Helper.ModRegistry.IsLoaded("FlashShifter.StardewValleyExpandedCP")
            || Helper.ModRegistry.IsLoaded("FlashShifter.StardewValleyExpandedALL")
            || Helper.ModRegistry.IsLoaded("FlashShifter.StardewValleyExpandedCode");
    }

    private static void GetExpandedNpcRosterPostfix(ref List<string> __result)
    {
        var mod = Instance;
        if (mod is null || __result is null)
            return;

        foreach (var name in mod.GetCompatibilityNpcNames())
        {
            if (!__result.Contains(name, StringComparer.OrdinalIgnoreCase))
                __result.Add(name);
        }
    }

    private static void BuildCompactGameStateInfoPostfix(string? npcName, ref string __result)
    {
        var mod = Instance;
        if (mod is null || string.IsNullOrWhiteSpace(__result))
            return;

        var additionalNames = mod.GetCompatibilityNpcNames();
        if (additionalNames.Count > 0)
        {
            var match = CanonNpcBlockRegex.Match(__result);
            if (match.Success)
            {
                var merged = new List<string>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var existing = match.Groups["list"].Value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var name in existing)
                {
                    if (seen.Add(name))
                        merged.Add(name);
                }

                foreach (var name in additionalNames)
                {
                    if (seen.Add(name))
                        merged.Add(name);
                }

                var replacement = $"CANON_NPCS: [{string.Join(", ", merged)}]";
                __result = CanonNpcBlockRegex.Replace(__result, replacement, 1);
            }
        }

        var loreBlock = mod.BuildSveLorePromptBlock(npcName);
        if (!string.IsNullOrWhiteSpace(loreBlock))
            __result = $"{__result} {loreBlock}".Trim();
    }

    private static bool TryFindNpcTargetInTextPrefix(string text, ref string? __result)
    {
        var mod = Instance;
        if (mod is null || string.IsNullOrWhiteSpace(text))
            return true;

        foreach (var name in mod.GetCompatibilityNpcNames().OrderByDescending(n => n.Length))
        {
            if (!ContainsNpcToken(text, name))
                continue;

            __result = NormalizeNpcToken(name);
            return false;
        }

        return true;
    }

    private static void ResolveNpcTargetFromTownEventPostfix(object ev, ref string __result)
    {
        if (!string.IsNullOrWhiteSpace(__result))
            return;

        var mod = Instance;
        if (mod is null || ev is null)
            return;

        var npcNames = mod.GetCompatibilityNpcNames();
        if (npcNames.Count == 0)
            return;

        foreach (var tag in ReadStringEnumerableProperty(ev, "Tags"))
        {
            var matched = npcNames.FirstOrDefault(n => string.Equals(tag, n, StringComparison.OrdinalIgnoreCase));
            if (matched is null)
                continue;

            __result = NormalizeNpcToken(matched);
            return;
        }

        var summary = ReadStringProperty(ev, "Summary");
        var location = ReadStringProperty(ev, "Location");
        var combined = $"{summary} {location}";

        if (string.IsNullOrWhiteSpace(combined))
            return;

        foreach (var name in npcNames.OrderByDescending(n => n.Length))
        {
            if (!ContainsNpcToken(combined, name))
                continue;

            __result = NormalizeNpcToken(name);
            return;
        }
    }

    private static string ReadStringProperty(object obj, string propertyName)
    {
        var prop = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop?.GetValue(obj) is string value)
            return value;

        return string.Empty;
    }

    private static IEnumerable<string> ReadStringEnumerableProperty(object obj, string propertyName)
    {
        var prop = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        var value = prop?.GetValue(obj);
        if (value is null)
            yield break;

        if (value is IEnumerable<string> typed)
        {
            foreach (var item in typed)
            {
                if (!string.IsNullOrWhiteSpace(item))
                    yield return item.Trim();
            }

            yield break;
        }

        if (value is not IEnumerable untyped)
            yield break;

        foreach (var item in untyped)
        {
            if (item is not string asString || string.IsNullOrWhiteSpace(asString))
                continue;

            yield return asString.Trim();
        }
    }

    private static bool ContainsNpcToken(string text, string targetName)
    {
        var normalized = NormalizeNpcToken(targetName).Replace("_", " ", StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        var escaped = Regex.Escape(normalized);
        var pattern = normalized.EndsWith("s", StringComparison.Ordinal)
            ? $@"\b{escaped}\b"
            : $@"\b{escaped}s?\b";

        return Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }

    private static string NormalizeNpcToken(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var normalized = raw
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", "_", StringComparison.Ordinal)
            .Replace("-", "_", StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace("'", string.Empty, StringComparison.Ordinal);

        normalized = Regex.Replace(normalized, @"[^a-z0-9_]+", string.Empty, RegexOptions.CultureInvariant);

        if (normalized.StartsWith("mr_", StringComparison.Ordinal))
            normalized = normalized[3..];
        else if (normalized.StartsWith("mrs_", StringComparison.Ordinal))
            normalized = normalized[4..];
        else if (normalized.StartsWith("ms_", StringComparison.Ordinal))
            normalized = normalized[3..];

        return normalized;
    }
}
