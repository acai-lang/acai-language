using System.CommandLine;

namespace Acai.src;

class UpdateCommand
{
    public static Command Create()
    {
        var updateCommand = new Command("update", "Checks for and installs updates for the Acai language engine.");

        // FIX: Primary name first (no spaces/commas), then use .Aliases.Add for short flags
        var channelOption = new Option<string>("--channel")
        {
            Description = "Specify the release channel: stable or beta.",
            HelpName = "channel",
            DefaultValueFactory = _ => "stable"
        };
        channelOption.Aliases.Add("-c"); // Safely adds the short alias without whitespace errors
        updateCommand.Options.Add(channelOption);

        updateCommand.SetAction(parseResult =>
        {
            var channel = parseResult.GetValue(channelOption)?.ToLower() ?? "stable";

            if (channel != "stable" && channel != "beta")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ Error: Invalid channel. Choose 'stable' or 'beta'.");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"🔄 [Acai Updater] Checking for updates on the '{channel}' channel...");
            Console.ResetColor();

            Updater.Execute(channel);
        });

        return updateCommand;
    }
}
