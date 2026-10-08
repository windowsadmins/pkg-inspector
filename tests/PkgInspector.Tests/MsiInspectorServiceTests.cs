using System.IO;
using System.Text;
using PkgInspector.Services;
using Xunit;

namespace PkgInspector.Tests;

/// <summary>
/// MsiInspectorService against real MSI databases shaped like cimipkg output:
/// base64 CIMIAN_PKG_BUILD_INFO, scripts in VBScript custom actions (format 1)
/// and scripts in the Binary table behind exe custom actions (format 2).
/// </summary>
public class MsiInspectorServiceTests : IDisposable
{
    private const string Yaml = "product:\n  name: Contoso Widget\n  version: 2026.10.08.0100\n  identifier: com.example.widget\n  developer: Contoso\ninstall_location: C:\\Program Files\\Widget\n";
    private const string PostinstallScript = "Write-Output 'hello from postinstall [x] {y}'\r\nexit 0";
    private const string UninstallScript = "Write-Output 'bye'";

    private readonly TestMsi _msis = new();

    public void Dispose() => _msis.Dispose();

    private static TestMsi.Builder BaseTables(TestMsi.Builder b, string buildInfoValue) => b
        .Sql("CREATE TABLE `Property` (`Property` CHAR(72) NOT NULL, `Value` LONGCHAR NOT NULL LOCALIZABLE PRIMARY KEY `Property`)")
        .Sql("CREATE TABLE `CustomAction` (`Action` CHAR(72) NOT NULL, `Type` SHORT NOT NULL, `Source` CHAR(72), `Target` LONGCHAR, `ExtendedType` LONG PRIMARY KEY `Action`)")
        .Sql("CREATE TABLE `Binary` (`Name` CHAR(72) NOT NULL, `Data` OBJECT NOT NULL PRIMARY KEY `Name`)")
        .Sql("INSERT INTO `Property` (`Property`, `Value`) VALUES ('ProductName', 'Contoso Widget')")
        .Sql("INSERT INTO `Property` (`Property`, `Value`) VALUES ('ProductCode', '{11111111-2222-3333-4444-555555555555}')")
        .Sql("INSERT INTO `Property` (`Property`, `Value`) VALUES (?, ?)", "CIMIAN_PKG_BUILD_INFO", buildInfoValue);

    [Fact]
    public async Task Base64BuildInfo_IsDecodedIntoMetadata()
    {
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(Yaml));
        var msi = _msis.Create(b => BaseTables(b, b64));

        var data = await new MsiInspectorService().InspectPackageAsync(msi);

        Assert.True(data.IsCimipkgMsi);
        Assert.Equal(Yaml, data.RawMetadata);
        Assert.Equal("Contoso Widget", data.Metadata!.Name);
        Assert.Equal("2026.10.08.0100", data.Metadata.Version);
        Assert.Equal("com.example.widget", data.Identifier);
    }

    [Fact]
    public async Task LegacyYamlBuildInfo_IsStillRead()
    {
        var msi = _msis.Create(b => BaseTables(b, Yaml));

        var data = await new MsiInspectorService().InspectPackageAsync(msi);

        Assert.Equal(Yaml, data.RawMetadata);
        Assert.Equal("2026.10.08.0100", data.Metadata!.Version);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("name: x", "name: x")]
    [InlineData("not base64!", "not base64!")]
    [InlineData("bmFtZTogeA==", "name: x")]
    public void DecodeBuildInfoYaml(string? input, string? expected)
    {
        Assert.Equal(expected, MsiInspectorService.DecodeBuildInfoYaml(input));
    }

    [Fact]
    public async Task Format2_ScriptsAreReadFromTheBinaryTable()
    {
        var msi = _msis.Create(b => BaseTables(b, Yaml)
            .Sql("INSERT INTO `CustomAction` (`Action`, `Type`, `Source`, `Target`) VALUES ('CimianPreinstall', 50, 'CIMIAN_PSEXE', '-Command x')")
            .Sql("INSERT INTO `CustomAction` (`Action`, `Type`, `Source`, `Target`) VALUES ('CimianPostinstall', 3122, 'CIMIAN_PSEXE', '-Command x')")
            .Sql("INSERT INTO `CustomAction` (`Action`, `Type`, `Source`, `Target`) VALUES ('CimianUninstall', 3186, 'CIMIAN_PSEXE', '-Command x')")
            .Sql("INSERT INTO `CustomAction` (`Action`, `Type`, `Source`, `Target`) VALUES ('SetCimianPsExe', 51, 'CIMIAN_PSEXE', 'powershell.exe')")
            .Binary("CimianPreinstall", TestMsi.EncodeFormat2Script("# No preinstall scripts"))
            .Binary("CimianPostinstall", TestMsi.EncodeFormat2Script(PostinstallScript))
            .Binary("CimianUninstall", TestMsi.EncodeFormat2Script(UninstallScript)));

        var data = await new MsiInspectorService().InspectPackageAsync(msi);

        // The placeholder preinstall is skipped, as it is for format 1.
        Assert.Equal(new[] { "postinstall.ps1", "uninstall.ps1" }, data.Scripts.Select(s => s.Name).Order().ToArray());
        Assert.Equal(PostinstallScript, data.Scripts.Single(s => s.Name == "postinstall.ps1").Content);
        Assert.Equal("Post-Install Script", data.Scripts.Single(s => s.Name == "postinstall.ps1").Type);
        Assert.Equal(UninstallScript, data.Scripts.Single(s => s.Name == "uninstall.ps1").Content);
    }

    [Fact]
    public async Task Format1_ScriptsAreStillDecodedFromVbs()
    {
        var msi = _msis.Create(b => BaseTables(b, Yaml)
            .Sql("INSERT INTO `CustomAction` (`Action`, `Type`, `Source`, `Target`) VALUES ('CimianPostinstall', 3110, '', ?)",
                TestMsi.EncodeFormat1Vbs(PostinstallScript)));

        var data = await new MsiInspectorService().InspectPackageAsync(msi);

        var script = Assert.Single(data.Scripts);
        Assert.Equal("postinstall.ps1", script.Name);
        Assert.Equal(PostinstallScript, script.Content);
    }

    [Theory]
    [InlineData(50, "CIMIAN_PSEXE", true)]
    [InlineData(3122, "CIMIAN_PSEXE", true)]
    [InlineData(3186, "CIMIAN_PSEXE", true)]
    [InlineData(51, "CIMIAN_PSEXE", false)]
    [InlineData(3110, "", false)]
    [InlineData(50, "OTHER_EXE", false)]
    public void ExeScriptActionDetection(int type, string source, bool expected)
    {
        Assert.Equal(expected, MsiInspectorService.IsCimipkgExeScriptAction(type, source));
    }

    [Fact]
    public async Task UnsignedMsi_IsReportedUnsigned()
    {
        var msi = _msis.Create(b => BaseTables(b, Yaml));

        var data = await new MsiInspectorService().InspectPackageAsync(msi);

        Assert.False(data.IsSigned);
        Assert.Equal(string.Empty, data.SignedBy);
    }

    [Fact]
    public void Authenticode_UnsignedFile_HasNoSignature()
    {
        var msi = _msis.Create(b => BaseTables(b, Yaml));

        var result = AuthenticodeVerifier.Verify(msi);

        Assert.False(result.HasSignature);
        Assert.False(result.IsTrusted);
    }
}
