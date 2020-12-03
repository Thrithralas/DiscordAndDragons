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
		
		internal static readonly string DND5EAPI = "https://www.dnd5eapi.co/api/";
		public async Task<string> HttpGet(string URL) {
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
			if (!Regex.IsMatch(args, @"^\d*d\d+((\+|-)\d+)?$")) {
				await ReplyAsync("Invalid diceroll!");
				return;
			}
			string[] diceAndPlus = args.Split(new char[]{'+', '-'});
			int extra = diceAndPlus.Length == 2 ? int.Parse(diceAndPlus[1]) : 0;
			string[] diceParams = diceAndPlus[0].Split('d');
			int multiplier = diceParams[0] == string.Empty ? 1 : int.Parse(diceParams[0]);
			if (Regex.IsMatch(args, @"d\d+-\d+$")) extra *= -1;
			int diceSize = int.Parse(diceParams[1]), nat20 = 0, nat1 = 0;
			int[] rolls = new int[multiplier];
			
			for (int i = 0; i < multiplier; i++) {
				rolls[i] = RandomNumberGenerator.GetInt32(1, diceSize+1);
				if (diceSize == 20) {
					if (rolls[i] == 20) nat20++;
					if (rolls[i] == 1) nat1++;
				}
			}

			if (multiplier != 1) {
				await ReplyAsync($"**Rolls:** {string.Join(", ", rolls)}\n**Sum:** {rolls.Sum()+extra}\n{(diceSize == 20 ? $"**Nat 20s:** {nat20}\n**Nat 1s:** {nat1}" : "")}");
			}
			else if (extra != 0){
				await ReplyAsync($"**Roll**: {rolls[0]}{(extra > 0 ? "+" : "")}{extra} = {rolls[0] + extra}");
			}
			else {
				await ReplyAsync($"**Roll:** {rolls[0]}");
			}
		}

		[Command("spell")]
		public async Task WSpell([Remainder] string args) {
			args = args.Replace(' ', '-').Replace("'",  "").ToLower();
			var htmlContent = await HttpGet("http://dnd5e.wikidot.com/spell:" + args);
			if (htmlContent.Contains("does not exist")) {
				await ReplyAsync("Spell not found!");
				return;
			}
			HtmlDocument doc = new HtmlDocument();
			doc.LoadHtml(htmlContent);
			HtmlNode node = doc.GetElementbyId("page-content");
			string name = doc.DocumentNode.SelectSingleNode("//div[@class='page-title page-header']").InnerText;
			string[] txt = node.InnerText.Split("\n").Where(s => s != string.Empty).ToArray();
			string[] edited = txt.Select(str => str.Contains(':') ? "**" + str.Insert(str.IndexOf(':'), "**") : str).Skip(2).Take(4).ToArray();
			string[] description = txt.Skip(6).Where(str => !str.Contains("At Higher Levels.") && !str.Contains("Spell Lists.")).ToArray();
			EmbedBuilder builder = new EmbedBuilder()
				.WithAuthor(name)
				.WithDescription(txt[1])
				.AddField("Stats", string.Join("\n", edited) + $"\n**Available to Classes:** {new string(txt.Last().Skip(13).ToArray())}");
			string currText = string.Empty;
			bool first = true;
			foreach (string s in description) {
				if (s.Length + currText.Length < 1022) currText += "\n\n" + s;
				else {
					builder.AddField(first ? "Description" : "‏‏‎ ‎", currText);
					currText = s;
					first = false;
				}
			}
			if (currText != string.Empty) {
				builder.AddField(first ? "Description" : "‎‎‏‏‎ ‎", currText);
			}

			string higherLevels = txt.Skip(6).Where(str => str.Contains("At Higher Levels.")).FirstOrDefault();
			if (higherLevels != default(string)) {
				builder.AddField("At Higher Levels", new string(higherLevels.Skip(18).ToArray()));
			}
			await ReplyAsync(embed: builder.Build());
		}

		[Command("calcweapon")]
		[Alias("cw")]
		public async Task Cw(string dice, int dexterity, int strength, [Remainder] string args) {
			StringBuilder stringBuilder = new StringBuilder();
			int atkbonus = 0;
			stringBuilder.Append("**ATK BONUS:** ");
			if (args.Contains("-p")) {
				if (args.Contains("-f") || (args.Contains("-r") && dexterity > strength)) {
					atkbonus = dexterity + int.Parse(args.Substring(args.IndexOf("-pb:")).Skip(4).First().ToString());
				}
				else {
					atkbonus = strength + int.Parse(args.Substring(args.IndexOf("-pb:")).Skip(4).First().ToString());

				}
			}
			else {
				if (args.Contains("-f") || (args.Contains("-r") && dexterity > strength)) {
					atkbonus = dexterity;
				}
				else {
					atkbonus = strength;

				}
			}
			stringBuilder.AppendLine((atkbonus > 0 ? "+" : "" ) + atkbonus.ToString());
			stringBuilder.Append("**Damage/Type:** " + dice);
			int dmgbonus = 0;
			if (args.Contains("-p")) {
				dmgbonus = atkbonus - int.Parse(args.Substring(args.IndexOf("-pb:")).Skip(4).First().ToString());
			}
			else dmgbonus = Math.Min(0, atkbonus);
			stringBuilder.AppendLine((dmgbonus > 0 ? "+" : "" ) + (dmgbonus != 0 ? dmgbonus.ToString() : ""));
			await ReplyAsync(stringBuilder.ToString());
		}
		
	}
	
}