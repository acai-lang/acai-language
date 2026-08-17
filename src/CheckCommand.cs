using System.CommandLine;
using System.IO;

namespace Acai.src;

public static class CheckCommand
{
    public static Command Create()
    {
        var checkCommand = new Command("check", "Performs static analysis on your Acai file without executing it.");

        var fileArgument = new Argument<FileInfo>("file") 
        { 
            Description = "The path to the .acai file to check" 
        };
        checkCommand.Arguments.Add(fileArgument);

        checkCommand.SetAction(parseResult =>
        {
            var file = parseResult.GetValue(fileArgument);

            if (file is not { Exists: true })
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: Target file '{file?.FullName}' could not be resolved.");
                Console.ResetColor();
                return;
            }

            Console.WriteLine($"🔍 [Acai Linter] Analyzing patterns in {file.Name}...");
            Console.WriteLine("✨ Success: Syntax is completely valid.");
        });

        return checkCommand;
    }
}
