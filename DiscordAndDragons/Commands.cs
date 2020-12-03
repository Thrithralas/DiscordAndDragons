using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using HtmlAgilityPack;

// ReSharper disable IdentifierTypo

namespace DiscordAndDragons {
	public class Commands : InteractiveBase {
		
		//GET request for website content, parsed with HTML Agility Pack
		private async Task<string> HttpGet(string URL) {
			
			using (var client = new HttpClient()) {
				using (var request = new HttpRequestMessage()) {
					
					request.RequestUri = new Uri(URL);
					request.Method = HttpMethod.Get;

					using (var response = await client.SendAsync(request)) {
						return await response.Content.ReadAsStringAsync();
					}
				}
			}
		}

		[Command("dice")]
		[Alias("roll", "r")]
		public async Task Roll([Remainder] string args) {

			//Dice roll validation. Format: MdV+O where M = Multiplier, V = Value, O = offset. M and O are optional and O can be negative.
			
			args = args.Replace(" ", "");
			if (!Regex.IsMatch(args, @"^\d*d\d+((\+|-)\d+)?$")) {
				await ReplyAsync("Invalid Dice Roll!");
				return;
			}
			
			//Temporary storage for string format of dice parameters
			
			string[] tempDiceStrings1 = args.Split(new char[]{'+', '-'});
			string[] tempDiceStrings2 = tempDiceStrings1[0].Split('d');
			
			//Generate numeric dice parameters
			
			int diceOffset = tempDiceStrings1.Length == 2 ? int.Parse(tempDiceStrings1[1]) : 0;
			int diceMultiplier = tempDiceStrings2[0] == string.Empty ? 1 : int.Parse(tempDiceStrings2[0]);
			int diceValue = int.Parse(tempDiceStrings2[1]);

			if (Regex.IsMatch(args, @"d\d+-\d+$")) diceOffset *= -1; //Determine sign of Offset
			
			int[] rolls = new int[diceMultiplier].Select(_ => RandomNumberGenerator.GetInt32(1, diceValue + 1)).ToArray(); //Generates an array of cryptographically safe numbers

			//Removed counting natural 20s and natural 1s - was unused
			
			if (diceMultiplier != 1) await ReplyAsync($"**Rolls:** {string.Join(", ", rolls)}\n**Sum:** {rolls.Sum()+diceOffset}");
			else if (diceOffset != 0) await ReplyAsync($"**Roll**: {rolls[0]}{(diceOffset > 0 ? "+" : "")}{diceOffset} = {rolls[0] + diceOffset}");
			else await ReplyAsync($"**Roll:** {rolls[0]}");
		}

		//Old .spell was deprecated, replaced with .wspell - no longer using generic API, HTML extraction used instead (from wikidot)
		
		[Command("spell")]
		public async Task Spell([Remainder] string args) {
			
			args = args.Replace(' ', '-').Replace("'",  "").ToLower(); //Prepare string for URL
			var htmlContent = await HttpGet("http://dnd5e.wikidot.com/spell:" + args);
			if (htmlContent.Contains("does not exist")) { //Simple way to determine spell not existing
				await ReplyAsync("Spell not found!");
				return;
			}
			
			//Establish HTML root node which contains the necessary parameters
			
			HtmlDocument doc = new HtmlDocument();
			doc.LoadHtml(htmlContent);
			HtmlNode node = doc.GetElementbyId("page-content");
			
			string name = doc.DocumentNode.SelectSingleNode("//div[@class='page-title page-header']").InnerText; //XPath selector for spell name
			
			//Format InnerText for display
			
			string[] rawData = node.InnerText.Split("\n").Where(s => s != string.Empty).ToArray();
			string[] formattedData = rawData.Select(str => str.Contains(':') ? "**" + str.Insert(str.IndexOf(':'), "**") : str).Skip(2).Take(4).ToArray();
			string[] spellDescriptionRaw = rawData.Skip(6).Where(str => !str.Contains("At Higher Levels.") && !str.Contains("Spell Lists.")).ToArray();
			
			//Construct the main parts of the embed that don't require further processing
			
			EmbedBuilder builder = new EmbedBuilder()
				.WithAuthor(name)
				.WithDescription(rawData[1])
				.AddField("Stats", string.Join("\n", formattedData) + $"\n**Available to Classes:** {new string(rawData.Last().Skip(13).ToArray())}");
			
			//Algorithm to fit as much of the description into a single block as possible. Discord limits 1024 characters per block.
			
			string currentField = string.Empty;
			bool firstEmbedBlock = true;
			
			foreach (string chunk in spellDescriptionRaw) {
				
				if (chunk.Length + currentField.Length < 1022) currentField += "\n\n" + chunk;
				else {
					builder.AddField(firstEmbedBlock ? "Description" : "‏‏‎ ‎", currentField);
					currentField = chunk;
					firstEmbedBlock = false;
				}
				
			}
			if (currentField != string.Empty) builder.AddField(firstEmbedBlock ? "Description" : "‎‎‏‏‎ ‎", currentField);

			//If spell has higher level variant, extend Embed
			
			string higherLevels = rawData.Skip(6).Where(str => str.Contains("At Higher Levels.")).FirstOrDefault();
			if (higherLevels != default(string)) {
				builder.AddField("At Higher Levels", new string(higherLevels.Skip(18).ToArray()));
			}
			await ReplyAsync(embed: builder.Build()); //Send embed to channel
		}

		//Calculates weapon hit bonus and damage bonus. Implemented due to Dorian not understanding the difference between the two
		
		[Command("calcweapon")]
		[Alias("cw")]
		public async Task Cw(string dice, int dexterity, int strength, [Remainder] string args) {
			
			/* Possible parameters:
			 * -f = Weapon is finesse, uses either Dexterity or Strength (whichever is higher)
			 * -r = Weapon is ranged/thrown, uses Dexterity
			 * -p = User is proficient with the weapon. In this case -pb is necessary
			 * -pb:NUM = A single digit number which represents the proficiency bonus of the user (can't be negative)
			 *
			 * By default calculator uses Strength
			 */
			
			StringBuilder stringBuilder = new StringBuilder().Append("**ATK BONUS:** ");
			int hitBonus = 0; 
			
			//Calculate raw hit bonus and apply proficiency if necessary
			
			if (args.Contains("-f") || args.Contains("-r") && dexterity > strength) hitBonus = dexterity;
			else hitBonus = strength;
			if (args.Contains("-p")) hitBonus += int.Parse(args.Substring(args.IndexOf("-pb:")).Skip(4).First().ToString());

			stringBuilder.AppendLine((hitBonus > 0 ? "+" : "" ) + hitBonus.ToString()).Append("**Damage/Type:** " + dice);
			
			//Evaluate damage bonus
			
			int damageBonus = 0;
			if (args.Contains("-p")) {
				damageBonus = hitBonus - int.Parse(args.Substring(args.IndexOf("-pb:")).Skip(4).First().ToString());
			}
			else damageBonus = Math.Min(0, hitBonus); //No attack bonus (or negative) if not proficient
			
			stringBuilder.AppendLine((damageBonus > 0 ? "+" : "" ) + (damageBonus != 0 ? damageBonus.ToString() : ""));
			await ReplyAsync(stringBuilder.ToString());
		}
		
	}
	
}