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

			Directory.CreateDirectory("./cache/spells"); //Creates directory for spell caching

			_client = new DiscordSocketClient(new DiscordSocketConfig());
			_client.Log += Log;

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

		private async Task Log(LogMessage l) {
			Console.WriteLine(l.Message);
		}
	}
}