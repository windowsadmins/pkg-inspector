using System.Text;
using System.Text.RegularExpressions;

namespace PkgInspector.Services;

/// <summary>
/// Decodes the PowerShell source embedded inside a cimipkg-built MSI
/// custom action VBScript.
///
/// cimipkg ships its preinstall/postinstall/uninstall PowerShell scripts as
/// an inline Type 102 VBScript custom action. The VBS chunks the script's
/// UTF-8-with-BOM base64 representation across many <c>b64 = b64 &amp; "..."</c>
/// lines (≤ 800 chars each to stay under the VBScript parser's per-line
/// limit), then at install time decodes it via MSXML <c>Msxml2.DOMDocument.6.0</c>
/// + <c>ADODB.Stream</c> into a temp .ps1 file and invokes
/// <c>powershell.exe -File</c> on it.
///
/// Showing the raw VBS to a user inspecting a package is useless — they want
/// to read what preinstall/postinstall.ps1 actually does, not stare at 40 KB
/// of base64. This helper performs the same decode offline so the Scripts
/// tab can display real PowerShell source, just like the .pkg / .nupkg path
/// already does.
/// </summary>
public static class CimipkgVbsDecoder
{
    // Matches the base64 chunk assignment lines emitted by cimipkg's
    // BuildScriptActionVbs. The regex tolerates any amount of interior
    // whitespace and accepts all standard base64 alphabet characters, so a
    // future cimipkg tweak (e.g. dropping the spaces around `=` or `&`)
    // does not break decoding.
    private static readonly Regex ChunkLine = new(
        "b64\\s*=\\s*b64\\s*&\\s*\"([A-Za-z0-9+/=]+)\"",
        RegexOptions.Compiled);

    /// <summary>
    /// Returns the decoded PowerShell source for a cimipkg custom action
    /// VBScript, or <c>null</c> if the target does not match the cimipkg
    /// chunked-base64 pattern.
    /// </summary>
    public static string? TryDecode(string? vbs)
    {
        if (string.IsNullOrEmpty(vbs)) return null;

        // Use the same whitespace-tolerant regex for the fast path so a
        // future cimipkg change to spacing (e.g. `b64=b64&"..."`) does not
        // get rejected by a literal-string prefilter even though the match
        // loop below would have accepted it. Running IsMatch once is cheap
        // relative to the full base64 decode + BOM check below, and it
        // keeps commercial-MSI inspection fast because those VBS bodies
        // never contain the chunk pattern at all.
        if (!ChunkLine.IsMatch(vbs)) return null;

        var builder = new StringBuilder(vbs.Length);
        foreach (Match match in ChunkLine.Matches(vbs))
        {
            builder.Append(match.Groups[1].Value);
        }
        if (builder.Length == 0) return null;

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(builder.ToString());
        }
        catch (FormatException)
        {
            // Chunks were present but didn't concatenate into valid base64.
            // Treat as "not a cimipkg script" so the caller falls back to
            // showing the raw VBS instead of throwing.
            return null;
        }

        // cimipkg prepends the 3-byte UTF-8 BOM so PowerShell 5.1 reads the
        // staged file reliably. Strip it if present so the returned string
        // is clean PS1 source without a stray BOM character at the top.
        var bom = Encoding.UTF8.GetPreamble();
        if (bytes.Length >= bom.Length &&
            bytes[0] == bom[0] && bytes[1] == bom[1] && bytes[2] == bom[2])
        {
            return Encoding.UTF8.GetString(bytes, bom.Length, bytes.Length - bom.Length);
        }

        return Encoding.UTF8.GetString(bytes);
    }
}
