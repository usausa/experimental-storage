namespace StorageServer.Client.Commands;

internal static class ConsoleHelper
{
    public static void WriteError(string message)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ForegroundColor = prev;
    }
}
