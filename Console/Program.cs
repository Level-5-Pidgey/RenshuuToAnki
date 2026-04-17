using System.Text;
using Spectre.Console.Cli;

namespace Console;

public class Program
{
	public static async Task<int> Main(string[] args)
	{
		System.Console.OutputEncoding = System.Text.Encoding.UTF8;
		var app = new CommandApp<RenshuuCommand>();
		app.Configure(config =>
		{
			config.SetApplicationName("renshuu-mnemonic-extractor");
			config.AddExample("--read-only --query=\"tag:Languages::Japanese::Writing::Kanji\"");
		});

		return await app.RunAsync(args);
	}
}