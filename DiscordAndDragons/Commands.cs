using System;
using System.Collections.Generic;
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

				EmbedBuilder builder = new EmbedBuilder()
					.WithAuthor(name)
					.WithDescription(rawData[1])
					.AddField("Stats", string.Join("\n", formattedData) + $"\n**Available to Classes:** {new string(rawData.Last().Skip(13).ToArray())}");
				spellDescriptionRaw.ToEmbed(builder);
				
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

		[Command("feature", RunMode = RunMode.Async)]
		public async Task Feature(string Class, [Remainder] string args) {

			//Argument here represents the name of the feature that was requested
			
			string subClass = "";
			string argument = args;
			
			//Subclass detection - will add a "**Subclass:** {SubclassName}**" into the embed
			
			if (args.Contains("s:")) {
				subClass = args.Split(' ')[0].Substring(2).Replace(' ', '-').Replace("'", "").ToLower();
				argument = string.Join(' ', args.Split(' ').Skip(1));
			}

			if (Class.Contains(':')) {
				string[] split = Class.Split(':');
				Class = split[0].ToLower();
				subClass = split[1].ToLower();
			}
			
			string idealArgument = "";
			foreach (string s in argument.Split(' ')) {
				if (s == "the" || s == "or" || s == "of") {
					idealArgument += $"{s} ";
					continue;
				}
				string lower = s.ToLower();
				idealArgument += char.ToUpper(lower[0]) + lower.Substring(1) + " ";
			}

			idealArgument = idealArgument[..^1];

			//Implemented Caching
			argument = argument.Replace(":", "").Replace(" ", "-");
			string path = "./cache/features/" + argument + ".xml";
			if (!File.Exists(path)) {
				
				string htmlContent = await HelperFunctions.HttpGet("http://dnd5e.wikidot.com/" + (subClass == "" ? Class : Class + ':' + subClass));

				HtmlDocument doc = new HtmlDocument();
				doc.LoadHtml(htmlContent);
				HtmlNode node = doc.DocumentNode.SelectSingleNode($@"//span[. ='{idealArgument}']"); //XPath for selection of correct Node

				if (node == null) {
					await ReplyAsync("No feature found!");
					return;
				}
				node = node.ParentNode;

				EmbedBuilder builder = new EmbedBuilder()
					.WithAuthor(idealArgument)
					//Turns the whole string to lowercase, except the first letter which is uppercase
					.WithDescription($"**Class:** {char.ToUpper(Class[0]) + Class.ToLower().Substring(1)}{(subClass != "" ? $"\n**Subclass: ** {char.ToUpper(subClass[0]) + subClass.ToLower().Substring(1)}" : "")}");

				//Since all features share one common parent, we have to go by siblings
				//A while loop could also work fyi
			
				List<string> messages = new List<string>();
				do {
					node = node.NextSibling;
					if (node.OriginalName != "table") messages.Add(node.InnerText.Replace("\n", ""));
				} while (!node.NextSibling.OriginalName.Contains('h'));

				messages.Where(s => s != "").ToEmbed(builder); //New function for embed building. Letf in array detection, no conflicts so far.
				HelperFunctions.Serialize(builder, path);
				await ReplyAsync(embed: builder.Build());
				return;
			}

			await ReplyAsync(embed: HelperFunctions.Deserialize<EmbedBuilder>(path).Build());

		}


	}

}