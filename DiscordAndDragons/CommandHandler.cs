using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace DiscordAndDragons {
	public class CommandHandler {
		private DiscordSocketClient _client;
		private CommandService _commandService;
		private readonly string prefix = ".";
		private IServiceProvider _services;

		public async Task SetHandler(DiscordSocketClient client, IServiceProvider provider) {

			_services = provider;
			_client = client;
			_commandService = new CommandService();

			_client.MessageReceived += ProcessCommand;

			await _commandService.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
			
		}
		private async Task ProcessCommand(SocketMessage s) {

			if (!(s is SocketUserMessage message)) return;
			var context = new SocketCommandContext(_client, message);
			int pos = 0;

			if (message.HasStringPrefix(prefix, ref pos) && message.Content[1] != ' ' && message.Content[1] != '.') {
				var result = await _commandService.ExecuteAsync(context, pos, _services);
				if (!result.IsSuccess) {
					if (result.Error == CommandError.UnknownCommand) await message.Channel.SendMessageAsync("Unknown command!");
					else {
						await context.Channel.SendMessageAsync($"**{result.ErrorReason}**");
					}
				}
			}
		} 
	}
}