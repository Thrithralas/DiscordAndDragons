using Discord;
using Discord.WebSocket;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Discord.Addons.Interactive;

namespace DungeonsAndDiscord {
	public class Startup {

		private DiscordSocketClient _client;
		private CommandHandler _handler;
		private const string Token = "NTA3MjQwNzk3Njk2MDMyNzkw.W9nizg.KvXTtjHvuu-kEfeoOGjOzyzlG88";
		private IServiceProvider _services;


		static void Main() => new Startup().StartAsync().GetAwaiter().GetResult();
        
		private async Task StartAsync() {

			_client = new DiscordSocketClient(new DiscordSocketConfig());
			_client.Log += Log;

			await _client.LoginAsync(TokenType.Bot, Token);
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