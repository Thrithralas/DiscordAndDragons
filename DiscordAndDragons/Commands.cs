﻿using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using HtmlAgilityPack;

// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo

namespace DiscordAndDragons {
	public class Commands : InteractiveBase {

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

			string[] tempDiceStrings1 = args.Split('+', '-');
			string[] tempDiceStrings2 = tempDiceStrings1[0].Split('d');

			//Generate numeric dice parameters

			int diceOffset = tempDiceStrings1.Length == 2 ? int.Parse(tempDiceStrings1[1]) : 0;
			int diceMultiplier = tempDiceStrings2[0] == string.Empty ? 1 : int.Parse(tempDiceStrings2[0]);
			int diceValue = int.Parse(tempDiceStrings2[1]);

			if (Regex.IsMatch(args, @"d\d+-\d+$")) diceOffset *= -1; //Determine sign of Offset

			int[] rolls = new int[diceMultiplier].Select(_ => RandomNumberGenerator.GetInt32(1, diceValue + 1)).ToArray(); //Generates an array of cryptographically safe numbers

			//Removed counting natural 20s and natural 1s - was unused

			if (diceMultiplier != 1) await ReplyAsync($"**Rolls:** {string.Join(", ", rolls)}\n**Sum:** {rolls.Sum() + diceOffset}");
			else if (diceOffset != 0) await ReplyAsync($"**Roll**: {rolls[0]}{(diceOffset > 0 ? "+" : "")}{diceOffset} = {rolls[0] + diceOffset}");
			else await ReplyAsync($"**Roll:** {rolls[0]}");
		}
		
		[Command("spell")]
		public async Task Spell([Remainder] string args) {
			args = args.Replace(' ', '-').Replace("'", "").ToLower(); //Prepare string for URL

			//Caching - checks if spell is already stored as XML
			if (!File.Exists("./cache/spells/" + args + ".xml")) {

				var htmlContent = await HelperFunctions.HttpGet("http://dnd5e.wikidot.com/spell:" + args);
				if (htmlContent.Contains("does not exist")) {
					//Simple way to determine spell not existing
					await ReplyAsync("Spell not found!");
					return;
				}

				//Establish HTML root node which contains the necessary parameters

				HtmlDocument doc = new HtmlDocument();
				doc.LoadHtml(htmlContent);
				HtmlNode node = doc.GetElementbyId("page-content");

				//Recursive enumeration to remove table headers

				foreach (var value in node.RecursiveEnumerator()) {
					if (value.OriginalName == "th") value.ParentNode.RemoveChild(value);
				}

				string name = doc.DocumentNode.SelectSingleNode("//div[@class='page-title page-header']").InnerText; //XPath selector for spell name

				//Format InnerText for display

				string[] rawData = node.InnerText.Split("\n").Where(s => s != string.Empty).ToArray();
				string[] formattedData = rawData.Select(str => str.Contains(':') ? "**" + str.Insert(str.IndexOf(':'), "**") : str).Skip(2).Take(4).ToArray();
				string[] spellDescriptionRaw = rawData.Skip(6).Where(str => !str.Contains("At Higher Levels.") && !str.Contains("Spell Lists.")).ToArray();

				//Construct the main parts of the embed that don't require further processing

				EmbedBuilder builder = new EmbedBuilder().WithAuthor(name).WithDescription(rawData[1]).AddField("Stats", string.Join("\n", formattedData) + $"\n**Available to Classes:** {new string(rawData.Last().Skip(13).ToArray())}");

				//Algorithm to fit as much of the description into a single block as possible. Discord limits 1024 characters per block.

				string currentField = string.Empty;
				bool firstEmbedBlock = true;
				bool arraySequence = false;
				const int arrayDetectionTreshold = 10;

				foreach (string chunk in spellDescriptionRaw) {
					//Implement array detection for spells like Command and Chaos Bolt

					if (chunk.Length < arrayDetectionTreshold) {
						if (arraySequence) {
							currentField += " " + chunk;
							arraySequence = false;
						}
						else arraySequence = true;
					}

					if (arraySequence) currentField += "\n\n**" + chunk + (chunk.Last() == '.' ? "" : " - ") + "**"; //Insecure in case of array overflow (1024 characters) - will tackle if problem
					else if (chunk.Length + currentField.Length < 1022 && chunk.Length >= arrayDetectionTreshold) currentField += "\n\n" + chunk;
					else if (chunk.Length >= arrayDetectionTreshold) {
						builder.AddField(firstEmbedBlock ? "Description" : "‏‏‎ ‎", currentField);
						currentField = chunk;
						firstEmbedBlock = false;
					}
				}

				if (currentField != string.Empty) builder.AddField(firstEmbedBlock ? "Description" : "‎‎‏‏‎ ‎", currentField);

				//If spell has higher level variant, extend Embed

				string higherLevels = rawData.Skip(6).FirstOrDefault(str => str.Contains("At Higher Levels."));
				if (higherLevels != default) {
					builder.AddField("At Higher Levels", new string(higherLevels.Skip(18).ToArray()));
				}

				HelperFunctions.Serialize(builder, "./cache/spells/" + args + ".xml");
				await ReplyAsync(embed: builder.Build()); //Send embed to channel
				return;
			}
			
			//Loads from cache, much faster and prevents rate limiting
			await ReplyAsync(embed: HelperFunctions.Deserialize<EmbedBuilder>("./cache/spells/" + args + ".xml").Build());

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
			int hitBonus;

			//Calculate raw hit bonus and apply proficiency if necessary

			if (args.Contains("-f") || args.Contains("-r") && dexterity > strength) hitBonus = dexterity;
			else hitBonus = strength;
			if (args.Contains("-p")) hitBonus += int.Parse(args.Substring(args.IndexOf("-pb:", StringComparison.Ordinal)).Skip(4).First().ToString());

			stringBuilder.AppendLine((hitBonus > 0 ? "+" : "") + hitBonus).Append("**Damage/Type:** " + dice);

			//Evaluate damage bonus

			int damageBonus;
			if (args.Contains("-p")) {
				damageBonus = hitBonus - int.Parse(args.Substring(args.IndexOf("-pb:", StringComparison.Ordinal)).Skip(4).First().ToString());
			}
			else damageBonus = Math.Min(0, hitBonus); //No attack bonus (or negative) if not proficient

			stringBuilder.AppendLine((damageBonus > 0 ? "+" : "") + (damageBonus != 0 ? damageBonus.ToString() : ""));
			await ReplyAsync(stringBuilder.ToString());
		}


	}

}