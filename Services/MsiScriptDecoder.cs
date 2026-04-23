using System.Text;
using System.Text.RegularExpressions;
using PkgInspector.Models;

namespace PkgInspector.Services;

/// <summary>
/// Reverses cimipkg's VBScript+base64 wrapper around PowerShell custom actions so
/// the Scripts tab shows the original .ps1 content instead of opaque VBS.
///
/// cimipkg's build-time pipeline (see MsiBuilder.BuildScriptActionVbs in cimipkg):
///   original .ps1 files → concatenated with "# === Included from: name ===" markers
///     → prepended with $payloadRoot/$payloadDir/$installLocation header
///     → signed (if cert configured, producing a "# SIG # Begin signature block" suffix)
///     → UTF-8 BOM prepended
///     → base64-encoded
///     → chunked into 800-char `b64 = b64 &amp; "..."` lines inside a VBS stub
///
/// This decoder unwinds that: extract chunks, base64-decode, strip BOM,
/// and split on the "Included from" markers to recover individual scripts.
/// </summary>
public static class MsiScriptDecoder
{
    private static readonly Regex ChunkRegex = new(
        @"b64\s*=\s*b64\s*&\s*""([^""]*)""",
        RegexOptions.Compiled);

    private static readonly Regex IncludedFromRegex = new(
        @"^#\s*===\s*Included from:\s*(?<name>.+?)\s*===\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>
    /// Returns true if the VBS source looks like a cimipkg-generated Type 102 wrapper.
    /// </summary>
    public static bool LooksLikeCimipkgWrapper(string vbsTarget)
    {
        if (string.IsNullOrEmpty(vbsTarget)) return false;
        return vbsTarget.Contains("b64 = b64 &", StringComparison.Ordinal) &&
               vbsTarget.Contains("Msxml2.DOMDocument", StringComparison.Ordinal);
    }

    /// <summary>
    /// Decode a single VBS custom-action body back into the combined PowerShell script.
    /// Returns null if the VBS doesn't match cimipkg's shape.
    /// </summary>
    public static string? DecodeCombinedScript(string vbsTarget)
    {
        if (!LooksLikeCimipkgWrapper(vbsTarget)) return null;

        var sb = new StringBuilder();
        foreach (Match m in ChunkRegex.Matches(vbsTarget))
        {
            sb.Append(m.Groups[1].Value);
        }

        var base64 = sb.ToString();
        if (base64.Length == 0) return null;

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return null;
        }

        // Strip UTF-8 BOM (cimipkg prepends one so PS 5.1 reads the staged file as UTF-8)
        int start = 0;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            start = 3;

        return Encoding.UTF8.GetString(bytes, start, bytes.Length - start);
    }

    /// <summary>
    /// Decode a cimipkg custom action into its constituent original scripts.
    /// Uses the "# === Included from: name.ps1 ===" markers cimipkg's ScriptProcessor
    /// emits when combining preinstall*/postinstall*/uninstall* files.
    ///
    /// If no markers are found (single-script action, or pre-marker cimipkg builds),
    /// the whole combined script is returned as one entry named after the action.
    /// </summary>
    public static List<ScriptInfo> DecodeToScripts(string actionName, string vbsTarget)
    {
        var results = new List<ScriptInfo>();
        var combined = DecodeCombinedScript(vbsTarget);
        if (string.IsNullOrEmpty(combined)) return results;

        var phase = ClassifyActionPhase(actionName);

        // Drop the cimipkg-injected $payloadRoot/$payloadDir/$installLocation
        // variable header — it's build-time wiring, not user-authored script.
        // If a future caller wants it, expose a flag; for now keep the list
        // to the author's own .ps1 files.
        var (_, body) = SplitHeader(combined);

        var markers = IncludedFromRegex.Matches(body);

        if (markers.Count == 0)
        {
            var content = body.TrimStart('\r', '\n');

            // cimipkg emits a "# No {phase} scripts" placeholder for actions that
            // exist in the sequence but have no user-authored script. Hide these —
            // the custom action is present in the MSI but there's nothing to show.
            if (IsCimipkgPlaceholder(content))
                return results;

            results.Add(new ScriptInfo
            {
                Name = $"{actionName}.ps1",
                Type = phase,
                Content = content,
                RelativePath = $"CustomAction/{actionName}"
            });
            return results;
        }

        for (int i = 0; i < markers.Count; i++)
        {
            var m = markers[i];
            var name = m.Groups["name"].Value.Trim();
            var contentStart = m.Index + m.Length;
            var contentEnd = i + 1 < markers.Count ? markers[i + 1].Index : body.Length;
            var content = body.Substring(contentStart, contentEnd - contentStart).TrimStart('\r', '\n');

            results.Add(new ScriptInfo
            {
                Name = name,
                Type = phase,
                Content = content,
                RelativePath = $"CustomAction/{actionName}/{name}"
            });
        }

        return results;
    }

    /// <summary>
    /// Split the cimipkg-injected variable header ($payloadRoot / $payloadDir /
    /// $installLocation assignments, optionally followed by a blank line) from
    /// the combined script body. Returns empty header if the shape doesn't match.
    /// </summary>
    private static (string header, string body) SplitHeader(string combined)
    {
        // The header is 3 assignment lines followed by a blank line, per
        // MsiBuilder.WriteScriptCustomActions. Find the first "# === Included from"
        // marker — everything before it is the header; everything from it on is body.
        var firstMarker = IncludedFromRegex.Match(combined);
        if (!firstMarker.Success)
            return (string.Empty, combined);

        var header = combined[..firstMarker.Index];
        var body = combined[firstMarker.Index..];
        return (header, body);
    }

    private static bool IsCimipkgPlaceholder(string content)
    {
        // cimipkg stubs are a single short comment line: "# No preinstall scripts",
        // "# No postinstall scripts", "# No uninstall scripts". Match the shape
        // rather than hard-coding phase names so future cimipkg stubs also collapse.
        var trimmed = content.Trim();
        return trimmed.StartsWith("# No ", StringComparison.Ordinal) &&
               trimmed.EndsWith(" scripts", StringComparison.Ordinal) &&
               !trimmed.Contains('\n');
    }

    private static string ClassifyActionPhase(string actionName)
    {
        if (actionName.Contains("Preinstall", StringComparison.OrdinalIgnoreCase))
            return "Pre-Install Script";
        if (actionName.Contains("Postinstall", StringComparison.OrdinalIgnoreCase))
            return "Post-Install Script";
        if (actionName.Contains("Uninstall", StringComparison.OrdinalIgnoreCase))
            return "Uninstall Script";
        return "Custom Action Script";
    }
}
