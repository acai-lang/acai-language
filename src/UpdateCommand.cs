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
            Description = "Specify the release channel: stable or beta. (required)",
            HelpName = "channel",
        };
        channelOption.Aliases.Add("-c");
        updateCommand.Options.Add(channelOption);

        updateCommand.SetAction(parseResult =>
        {
            var channel = parseResult.GetValue(channelOption)?.ToLower();

            if (string.IsNullOrWhiteSpace(channel) || (channel != "stable" && channel != "beta"))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ Error: Invalid channel. Choose 'stable' or 'beta'.");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"🔄 [Acai Updater] Checking for updates on the '{channel}' channel...");
            Console.ResetColor();

            // Map CLI channel to Updater channel token and call the async updater synchronously
            // For channel updates perform a fresh installation using the ZeroCloud upgrader
            var upgraderChannel = channel == "beta" ? "beta" : "stable";
            Acai.src.AcaiZeroCloudUpgrader.RunAsync(upgraderChannel, true).GetAwaiter().GetResult();
        });

        return updateCommand;
    }
}

// The actual update implementation lives in src/Updater.cs as `Acai.src.Updater`.
