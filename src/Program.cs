using System.CommandLine;
using System.IO;
using Acai.src;

// 1. Create the main root command for the executable 'acai'
var rootCommand = new RootCommand("Acai Language Runtime Environment Engine.");

// 2. Add the file argument directly to the root command (making it optional so help still works)
var fileArgument = new Argument<FileInfo?>("file")
{
    Description = "The path to the .acai source file to execute.",
    // Setting arity to ZeroOrOne ensures typing just 'acai' or 'acai -h' doesn't fail due to a missing file
    Arity = ArgumentArity.ZeroOrOne
};
rootCommand.Arguments.Add(fileArgument);

// 3. Keep your global options like verbose on the root command level
var verboseOption = new Option<bool>("--verbose") 
{ 
    Description = "Display complete parser and token logs." 
};
verboseOption.Aliases.Add("-v");
rootCommand.Options.Add(verboseOption);

// 4. Register your remaining subcommands (check and update)
rootCommand.Subcommands.Add(CheckCommand.Create());
rootCommand.Subcommands.Add(UpdateCommand.Create());

// 5. Define what happens when a user runs the root 'acai' command directly
rootCommand.SetAction(parseResult =>
{
    var file = parseResult.GetValue(fileArgument);
    var verbose = parseResult.GetValue(verboseOption);

    // If the user didn't provide a file (e.g. they just typed 'acai'), show global help
    if (file == null)
    {
        // System.CommandLine automatically displays help when no action or arguments match,
        // but since we made the argument optional, we force help to print if no file is given.
        Console.WriteLine(rootCommand.Description);
        Console.WriteLine("\nUsage:\n  acai <file> [options]");
        Console.WriteLine("\nCommands:\n  check   Performs static analysis.\n  update  Checks for updates.");
        return;
    }

    // Strict Extension Validation (.acai only)
    if (file.Extension.ToLower() != ".acai")
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: Invalid file format '{file.Extension}'. Acai runtime only executes '.acai' source files.");
        Console.ResetColor();
        return;
    }

    // Check if the file physically exists on disk
    if (!file.Exists)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: The file '{file.FullName}' does not exist.");
        Console.ResetColor();
        return;
    }

    // Read the source text and transition execution directly to your engine
    try
    {
        string sourceCode = File.ReadAllText(file.FullName);

        if (verbose)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"[Debug] Loaded file: {file.Name} ({sourceCode.Length} characters)");
            Console.ResetColor();
        }

        // =============================================================
        // 👉 YOUR INTERPRETER STARTS HERE!
        // =============================================================
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"⚡ [Acai Engine] Running pipeline directly for: {file.Name}");
        Console.ResetColor();

        // 1. Pass the raw file code straight into your Lexer loop 
        var lexer = new Lexer(sourceCode);
        var tokens = lexer.Tokenize();

        if (verbose)
        {
            Console.WriteLine("\n--- [Debug] Token List Output ---");
            foreach (var token in tokens)
            {
                Console.WriteLine($"Type: {token.Type,-18} | Value: {token.Value}");
            }
            Console.WriteLine("--------------------------------\n");
        }

        // 2. Feed those generated sequential patterns straight into your grammar Parser
        var parser = new Parser(tokens, file.DirectoryName);
        parser.ParseAndExecute();
        // =============================================================

        // =============================================================
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: Failed to read file content. {ex.Message}");
        Console.ResetColor();
    }
});

// Execute the command line args parsing loop natively
return rootCommand.Parse(args).Invoke();
