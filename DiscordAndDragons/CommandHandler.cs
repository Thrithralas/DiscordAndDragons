using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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

			_commandService.AddTypeReader<List<DiceRoll>>(new DiceTypeReader());
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

	//TypeReader for List<DiceRoll>
	public class DiceTypeReader : TypeReader {
		public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services) {
			
			//Splits by addition first, then subtraction to differentiate the pseudoMultiplier
			string[] inputs = input.Split(new[] {"+"}, StringSplitOptions.RemoveEmptyEntries);
			
			if (inputs.Length == 0) return GenerateCommandError();
			List<DiceRoll> diceRolls = new();

			foreach (var t in inputs) {
				string current = t;

				string[] negSplit = current.Split('-', StringSplitOptions.RemoveEmptyEntries);
				for (int j = 0; j < negSplit.Length; j++) {
					int pseudoMultiplier = j > 0 ? -1 : 1; //Splitting something like A-B-C will result in A being positive, thus its ignored sign-wise
					current = negSplit[j];

					if (int.TryParse(current, out int dValue)) { //Constant value handler
						diceRolls.Add(new DiceRoll() {Multiplier = 0, Constant = dValue * pseudoMultiplier});
						continue;
					}
					
					if (!Regex.IsMatch(current, @"^[AD]?\d*d\d+G?$")) { //Regex to match all dices but constants
						return GenerateCommandError();
					}

					DiceRoll d = new DiceRoll() {Advantage = current[0] == 'A', Disadvantage = current[0] == 'D'};

					if (!char.IsDigit(current[0]) && current[0] != 'd') current = current[1..]; //Remove first digit if A or D

					if (current.EndsWith('G')) { //Average handler
						current = current[..^1];
						d.WithAverage = true;
					}

					if (current[0] == 'd') { //No Multiplier handler
						d.Multiplier = pseudoMultiplier;
						current = current[1..];
					}
					else {
						d.Multiplier = int.Parse(current[..current.IndexOf('d')]) * pseudoMultiplier;
						current = current[(current.IndexOf('d') + 1)..];
					}

					d.DiceValue = int.Parse(current);
				
					diceRolls.Add(d);
				}
			}

			return Task.FromResult(TypeReaderResult.FromSuccess(diceRolls));
		}

		private Task<TypeReaderResult> GenerateCommandError() {
			return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Dice Rolls could not be parsed"));
		}
	}
}