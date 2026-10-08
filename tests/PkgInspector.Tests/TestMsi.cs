using System.IO;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace PkgInspector.Tests;

/// <summary>
/// Builds real MSI databases for tests through msi.dll: SQL statements with
/// string parameters, and Binary rows from bytes. Files are deleted on Dispose.
/// </summary>
public sealed class TestMsi : IDisposable
{
    private readonly List<string> _paths = new();

    public string Create(Action<Builder> build)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pkginspector_{Guid.NewGuid():N}.msi");
        _paths.Add(path);
        Check(MsiOpenDatabaseW(path, (IntPtr)3, out var db));
        try
        {
            build(new Builder(db, _paths));
            Check(MsiDatabaseCommit(db));
        }
        finally
        {
            MsiCloseHandle(db);
        }
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _paths)
        {
            try { File.Delete(path); } catch { }
        }
    }

    public sealed class Builder
    {
        private readonly IntPtr _db;
        private readonly List<string> _paths;

        internal Builder(IntPtr db, List<string> paths) { _db = db; _paths = paths; }

        public Builder Sql(string sql, params string[] args)
        {
            Check(MsiDatabaseOpenViewW(_db, sql, out var view));
            var record = IntPtr.Zero;
            try
            {
                if (args.Length > 0)
                {
                    record = MsiCreateRecord((uint)args.Length);
                    for (var i = 0; i < args.Length; i++)
                    {
                        Check(MsiRecordSetStringW(record, (uint)(i + 1), args[i]));
                    }
                }
                Check(MsiViewExecute(view, record));
            }
            finally
            {
                if (record != IntPtr.Zero) MsiCloseHandle(record);
                MsiCloseHandle(view);
            }
            return this;
        }

        public Builder Binary(string name, byte[] data)
        {
            var file = Path.Combine(Path.GetTempPath(), $"pkginspector_{Guid.NewGuid():N}.bin");
            _paths.Add(file);
            File.WriteAllBytes(file, data);
            Check(MsiDatabaseOpenViewW(_db, "SELECT `Name`, `Data` FROM `Binary`", out var view));
            var record = MsiCreateRecord(2);
            try
            {
                Check(MsiViewExecute(view, IntPtr.Zero));
                Check(MsiRecordSetStringW(record, 1, name));
                Check(MsiRecordSetStreamW(record, 2, file));
                Check(MsiViewModify(view, 3, record));
            }
            finally
            {
                MsiCloseHandle(record);
                MsiCloseHandle(view);
            }
            return this;
        }
    }

    /// <summary>cimipkg's script format 2 encoding: base64 of UTF-8 with a BOM.</summary>
    public static byte[] EncodeFormat2Script(string script) =>
        Encoding.ASCII.GetBytes(Convert.ToBase64String(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(script)).ToArray()));

    /// <summary>cimipkg's script format 1 shape: the base64 in chunked VBS lines.</summary>
    public static string EncodeFormat1Vbs(string script)
    {
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(script)).ToArray());
        var vbs = new StringBuilder("On Error Resume Next\r\nb64 = \"\"\r\n");
        for (var i = 0; i < b64.Length; i += 800)
        {
            vbs.Append("b64 = b64 & \"").Append(b64, i, Math.Min(800, b64.Length - i)).Append("\"\r\n");
        }
        return vbs.ToString();
    }

    private static void Check(uint result)
    {
        if (result != 0) throw new Win32Exception((int)result);
    }

    [DllImport("msi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint MsiOpenDatabaseW(string path, IntPtr persist, out IntPtr db);

    [DllImport("msi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint MsiDatabaseOpenViewW(IntPtr db, string query, out IntPtr view);

    [DllImport("msi.dll", ExactSpelling = true)]
    private static extern uint MsiViewExecute(IntPtr view, IntPtr record);

    [DllImport("msi.dll", ExactSpelling = true)]
    private static extern uint MsiViewModify(IntPtr view, int mode, IntPtr record);

    [DllImport("msi.dll", ExactSpelling = true)]
    private static extern IntPtr MsiCreateRecord(uint count);

    [DllImport("msi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint MsiRecordSetStringW(IntPtr record, uint field, string value);

    [DllImport("msi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint MsiRecordSetStreamW(IntPtr record, uint field, string path);

    [DllImport("msi.dll", ExactSpelling = true)]
    private static extern uint MsiDatabaseCommit(IntPtr db);

    [DllImport("msi.dll", ExactSpelling = true)]
    private static extern uint MsiCloseHandle(IntPtr handle);
}
