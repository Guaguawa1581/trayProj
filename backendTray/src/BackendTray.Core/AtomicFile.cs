using System.Text;

namespace BackendTray.Core;

internal static class AtomicFile
{
    /// <summary>先寫暫存檔並寫入磁碟，再取代目標檔，避免寫到一半斷電造成檔案毀損。</summary>
    public static void WriteAllText(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";

        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(path))
            File.Replace(temporary, path, destinationBackupFileName: null);
        else
            File.Move(temporary, path);
    }
}
