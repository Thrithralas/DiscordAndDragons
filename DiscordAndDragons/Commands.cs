using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

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

		[Command("cdice")]
		[Alias("croll", "cr")]
		public async Task NewDice(List<DiceRoll> dice) {
			
			//Alternative dice formatting algorithm (implented by TypeReader, see CommandHandler.cs)
			
			EmbedBuilder builder = new EmbedBuilder().WithAuthor("🎲 Roll Statistics");
			List<int> values = new();
			
			//Indicate the three columns displayed inline by embed
			
			string valueColumn = "";
			string advDisadvAvgColumn = "";
			string resultColumn = "";
			
			//Did switch instead of elif by accident because wanted to split case -1 and 1
			
			foreach (DiceRoll d in dice) {
				switch (Math.Sign(d.Multiplier)) {
					case 1:
					case -1:
						
						valueColumn += d.Multiplier + "d" + d.DiceValue + "\n";

						advDisadvAvgColumn += (d.WithAverage ? "Average" : d.Advantage ? "Advantage" : d.Disadvantage ? "Disadvantage" : "None") + "\n";
						
						int value = d.Evaluate();
						resultColumn += value + "\n";
						values.Add(value);
						break;
					
					case 0:
						
						valueColumn += "None\n";
						advDisadvAvgColumn += "None\n";
						resultColumn += d.Evaluate() + "\n"; //Constants always return the same
						
						values.Add(d.Evaluate());
						break;
					
				}
			}

			builder
				.AddField("Dice Values", valueColumn, true)
				.AddField("Interference", advDisadvAvgColumn, true)
				.AddField("Result", resultColumn, true).AddField("Total", values.Sum().ToString());
			await ReplyAsync(embed: builder.Build());
		}
		
		[Command("spell")]
		public async Task Spell([Remainder] string args) {
			args = args.Replace(' ', '-').Replace("'", "").ToLower(); //Prepare string for URL

			//Caching - checks if spell is already stored as XML
			if (!File.Exists("./cache/spells/" + args + ".xml")) {

				var htmlContent = await HelperFunctions.HttpGet("http://dnd5e.wikidot.com/spell:" + args);
				if (htmlContent == "404") {
					//Simple way to determine spell not existing
					await ReplyAsync("Spell not found!");
					return;
				}

				//Establish HTML root node which contains the necessary parameters

				HtmlDocument doc = new();
				doc.LoadHtml(htmlContent);
				HtmlNode node = doc.GetElementbyId("page-content");

				//Recursive enumeration to remove table headers

				foreach (var value in node.RecursiveEnumerator()) {
					if (
						value.OriginalName == "th" ||
						string.IsNullOrEmpty(value.InnerText) ||
						Regex.IsMatch(value.InnerText, @"^\n{2,}$") ||
						value.OriginalName == "table")
						
						value.ParentNode.RemoveChild(value);
				}

				string name = doc.DocumentNode.SelectSingleNode("//div[@class='page-title page-header']").InnerText; //XPath selector for spell name
				node.RemoveChild(node.FirstChild);

				//Build predetermined part of the Embed
				
				EmbedBuilder builder = new EmbedBuilder()
					.WithAuthor(name)
					.WithDescription(node.ChildNodes[1].InnerText)
					.AddField("Stats", node.ChildNodes[3].InnerHtml.Replace("<strong>", "**").Replace("</strong>", "**") + $"\n**Available to Classes:** {node.LastChild.PreviousSibling.InnerText[13..]}");

				//Evaluate Description
				
				HtmlNode currentNode = node.ChildNodes[5];
				string nxtInnerText;
				string outputText = "";
				bool isFirst = true;
				
				do {
					if (currentNode.Name == "ul") { // Checks for list
						outputText += (currentNode.PreviousSibling.Name == "ul" ? "" : "\n") + "⠀•⠀";
					}
					
					outputText += currentNode.InnerText[(currentNode.Name == "ul" ? 1 : 0)..];
					currentNode = currentNode.NextSibling;
					nxtInnerText = currentNode.InnerText;
					
					if (nxtInnerText.Length + outputText.Length > 1018) { //6 spaces of safety margin
						builder.AddField(isFirst ? "Description" : "⠀", outputText); //Discord doesn't allow empty field names - unicode workaround
						isFirst = false;
						outputText = "";
					} 
				}
				while (!nxtInnerText.Contains("At Higher Levels.") && !nxtInnerText.Contains("Spell Lists."));

				if (outputText != string.Empty) {
					builder.AddField(isFirst ? "Description" : "⠀", outputText);
				}
				
				//Add higher levels if it exists
				
				if (nxtInnerText.Contains("At Higher Levels")) {
					builder.AddField("At Higher Levels", nxtInnerText[18..]);
				}
				
				await ReplyAsync(embed: builder.Build());
				HelperFunctions.Serialize(builder, $"./cache/spells/{args}.xml");
				
				return;
			}
			
			//Loads from cache, much faster and prevents rate limiting
			await ReplyAsync(embed: HelperFunctions.Deserialize<EmbedBuilder>($"./cache/spells/{args}.xml").Build());

		}


		[Command("feature", RunMode = RunMode.Async)]
		public async Task Feature(string className, [Remainder] string args) {

			//Argument here represents the name of the feature that was requested
			
			string subClass = "";
			string argument = args;
			
			//Subclass detection - will add a "**Subclass:** {SubclassName}**" into the embed
			
			if (args.Contains("s:")) {
				subClass = args.Split(' ')[0][2..].Replace(' ', '-').Replace("'", "").ToLower();
				argument = string.Join(' ', args.Split(' ')[1..]);
			}

			if (className.Contains(':')) {
				string[] split = className.Split(':');
				className = split[0].ToLower();
				subClass = split[1].ToLower();
			}
			
			string idealArgument = "";
			foreach (string s in argument.Split(' ')) {
				if (s == "the" || s == "or" || s == "of") {
					idealArgument += $"{s} ";
					continue;
				}
				string lower = s.ToLower();
				idealArgument += char.ToUpper(lower[0]) + lower[1..] + " ";
			}

			idealArgument = idealArgument[..^1];

			//Implemented Caching
			argument = argument.Replace(":", "").Replace(" ", "-");
			string path = "./cache/features/" + argument + ".xml";
			if (!File.Exists(path)) {
				
				string htmlContent = await HelperFunctions.HttpGet("http://dnd5e.wikidot.com/" + (subClass == "" ? className : className + ':' + subClass));

				HtmlDocument doc = new();
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
					.WithDescription($"**Class:** {char.ToUpper(className[0]) + className.ToLower()[1..]}{(subClass != "" ? $"\n**Subclass: ** {char.ToUpper(subClass[0]) + subClass.ToLower()[1..]}" : "")}");

				//Since all features share one common parent, we have to go by siblings
				//A while loop could also work fyi
			
				List<string> messages = new();
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

		[Command("spells", RunMode = RunMode.Async)]
		public async Task GetSpells(string className, int level, int page = 1) {

			string htmlContent = await HelperFunctions.HttpGet("http://dnd5e.wikidot.com/spells:" + className.ToLower());
			if (htmlContent == "404") {
				await ReplyAsync("Incorrect class!");
				return;
			}

			//Establish HTML root node which contains the necessary parameters

			HtmlDocument document = new();
			document.LoadHtml(htmlContent);
			HtmlNode node = document.DocumentNode.SelectSingleNode("//div[@class='yui-content']");
			
			//Get correct table

			HtmlNode tableNode = node.SelectSingleNode($"//div[@id='wiki-tab-0-{level}']");
			if (tableNode == null) {
				await ReplyAsync("Illegal level!");
				return;
			}

			tableNode = tableNode.FirstChild.ChildNodes[1];
			
			//Breaks children into blocks of 10

			IList<HtmlNode> spellNodes = tableNode.ChildNodes;
			spellNodes = spellNodes.Where(n => !(n is HtmlTextNode)).ToList();
			spellNodes.RemoveAt(0); //Removes header

			int maxPages = spellNodes.Count / 10;
			if (spellNodes.Count % 10 != 0) maxPages++;
			page = page % (maxPages + 1) - 1;

			//Initialize fields

			EmbedFieldBuilder spellName = new() {IsInline = true, Name = "Name"};
			EmbedFieldBuilder spellRange = new() {IsInline = true, Name = "Range"};
			EmbedFieldBuilder spellCastingTime = new() {IsInline = true, Name = "Casting Time"};

			for (int i = page * 10; i < page * 10 + (page + 1 == maxPages ? spellNodes.Count % 10 : 10); i++) {
				spellName.Value += spellNodes[i].ChildNodes[1].ChildNodes[0].InnerText + '\n';
				spellRange.Value += spellNodes[i].ChildNodes[7].InnerText + '\n';
				spellCastingTime.Value += spellNodes[i].ChildNodes[5].InnerText + '\n';
			}

			var builder = new EmbedBuilder().AddField(spellName).AddField(spellRange).AddField(spellCastingTime);
			builder.Title = char.ToUpper(className[0]) + className[1..].ToLower();
			if (level == 0) builder.Title += " Cantrips";
			else builder.Title += $" Level {level} Spells";
			builder.Title += $" (Page {page+1}/{maxPages})";
			await ReplyAsync(embed: builder.Build());

		}

		[Command("monster", RunMode = RunMode.Async)]
		public async Task GetMonster([Remainder] string name) {
			
			//Monsters use dynamic, because they are JSON Objects
			
			dynamic monster = await HelperFunctions.QueryMonsterByName(name);
			string speed = string.Empty;
			JObject jsonObject = (JObject) monster.speed;
			using var enumerator = jsonObject.GetEnumerator();
			enumerator.MoveNext(); //First element is empty bc fuck me am I right
			while (true) {
				var jsonProperty = enumerator.Current;
				if (jsonProperty.Key == "walk") speed += (string) jsonProperty.Value + " ft.";
				else speed += jsonProperty.Key + " " + (string) jsonProperty.Value + " ft.";
				if (!enumerator.MoveNext()) {
					break;
				}
				speed += ", ";
			}

			EmbedBuilder builder = new EmbedBuilder()
				.WithAuthor((string) monster.name)
				.WithThumbnailUrl((string) await HelperFunctions.GetMonsterImageUrl(monster)) //Monster image URl requires 5etools API Version
				.WithDescription($"*{(string) HelperFunctions.GetMonsterSizeRaceAndAlignment(monster)}*\n**Armour Class:** {(string) monster.ac[0].ac}\n**Hit Points:** {(string) monster.hp.average} ({(string) monster.hp.formula})\n**Speed:** {speed}\n**Challenge Rating:** {(string) monster.cr} ({HelperFunctions.CRToXP[(string) monster.cr]} XP)")
				.AddField("Strength", $"{(int) monster.str} ({((int) monster.str).CalculateBonus():+0;-#})", true)
				.AddField("Dexterity", $"{(int) monster.dex} ({((int) monster.dex).CalculateBonus():+0;-#})", true)
				.AddField("Constitution", $"{(int) monster.con} ({((int) monster.con).CalculateBonus():+0;-#})", true)
				.AddField("Intelligence", $"{(int) ((JToken) monster)["int"]} ({((int) ((JToken) monster)["int"]).CalculateBonus():+0;-#})", true)
				.AddField("Wisdom", $"{(int) monster.wis} ({((int) monster.wis).CalculateBonus():+0;-#})", true)
				.AddField("Charisma", $"{(int) monster.cha} ({((int) monster.cha).CalculateBonus():+0;-#})", true);
			await ReplyAsync(embed: builder.Build());
		}

	}

}