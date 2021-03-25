using Discord;
using Discord.WebSocket;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Discord.Addons.Interactive;

namespace DiscordAndDragons {
	public class Startup {

		private DiscordSocketClient _client;
		private CommandHandler _handler;
		private IServiceProvider _services;


		static void Main(string[] args) => new Startup().StartAsync(args[0]).GetAwaiter().GetResult();
        
		private async Task StartAsync(string token) {

			//Creates cache directories
			Directory.CreateDirectory("./cache/spells");
			Directory.CreateDirectory("./cache/features");
			Directory.CreateDirectory("./cache/monsters");

			_client = new DiscordSocketClient(new DiscordSocketConfig());
			_client.Log += LogAsync;

			await _client.LoginAsync(TokenType.Bot, token);
			await _client.StartAsync();

			_handler = new CommandHandler();
			_services = new ServiceCollection()
				.AddSingleton(_client)
				.AddSingleton(_handler)
				.AddSingleton(new InteractiveService(_client))
				.BuildServiceProvider();
			await _handler.SetHandler(_client, _services);
			await Task.Delay(-1);

		}

		internal static async Task LogAsync(LogMessage l) {
			switch (l.Severity) {
				case LogSeverity.Info:
					Console.ForegroundColor = ConsoleColor.White;
					break;
				case LogSeverity.Verbose:
					Console.ForegroundColor = ConsoleColor.Gray;
					break;
				case LogSeverity.Debug:
					Console.ForegroundColor = ConsoleColor.Green;
					break;
				case LogSeverity.Warning:
					Console.ForegroundColor = ConsoleColor.Yellow;
					break;
				case LogSeverity.Error:
					Console.ForegroundColor = ConsoleColor.Red;
					break;
				case LogSeverity.Critical:
					Console.ForegroundColor = ConsoleColor.DarkRed;
					break;
			}

			TimeSpan now = DateTime.Now.TimeOfDay;
			string msg = (l.Message == string.Empty ? l.Exception.StackTrace : l.Message) ?? "Missing stack trace.";
			await Console.Out.WriteLineAsync($"[{now:hh\\:mm\\:ss\\.ff} | {l.Source} | {l.Severity}] {msg}");
		}
	}
}