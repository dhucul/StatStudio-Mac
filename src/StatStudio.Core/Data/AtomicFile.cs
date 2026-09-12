namespace StatStudio.Core.Data;

/// <summary>Publish a complete replacement only after the writer succeeds.</summary>
public static class AtomicFile
{
    public static void Write(string path, Action<string> write)
    {
        string fullPath = Path.GetFullPath(path);
        string temp = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp" + Path.GetExtension(path);
        try
        {
            write(temp);
            File.Move(temp, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
