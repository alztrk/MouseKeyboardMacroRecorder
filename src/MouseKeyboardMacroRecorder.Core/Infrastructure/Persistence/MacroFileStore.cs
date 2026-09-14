using System.Text;
using MouseKeyboardMacroRecorder.Core.Domain;

namespace MouseKeyboardMacroRecorder.Core.Infrastructure.Persistence;

/// <summary>
/// Reads and writes macro files with same-directory atomic replacement.
/// </summary>
public sealed class MacroFileStore
{
    public const string FileExtension = ".macro.json";

    public static void Save(string path, MacroDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        MacroValidator.EnsureValid(document);
        EnsureExtension(path);
        try
        {
            AtomicFileWriter.WriteUtf8(path, MacroJsonSerializer.Serialize(document));
        }
        catch (IOException exception)
        {
            throw new MacroPersistenceException("The macro file could not be saved.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new MacroPersistenceException("The macro file could not be saved because access was denied.", exception);
        }
    }

    public static MacroDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        try
        {
            var json = File.ReadAllText(fullPath, Encoding.UTF8);
            return MacroJsonSerializer.Deserialize(json);
        }
        catch (FileNotFoundException exception)
        {
            throw new MacroPersistenceException("The macro file was not found.", exception);
        }
        catch (DirectoryNotFoundException exception)
        {
            throw new MacroPersistenceException("The macro file directory was not found.", exception);
        }
        catch (IOException exception)
        {
            throw new MacroPersistenceException("The macro file could not be read.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new MacroPersistenceException("The macro file could not be read because access was denied.", exception);
        }
    }

    private static void EnsureExtension(string path)
    {
        if (!path.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new MacroPersistenceException("Macro files must use the .macro.json extension.");
        }
    }
}

/// <summary>
/// Indicates that a macro file could not be read or written.
/// </summary>
public sealed class MacroPersistenceException : Exception
{
    public MacroPersistenceException(string message)
        : base(message)
    {
    }

    public MacroPersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
