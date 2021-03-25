using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Discord;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DiscordAndDragons {
	public static class HelperFunctions {

		private static readonly int ArrayDetectionThreshold = 10; //ArrayDetectionThreshold is now a static constant
		private static readonly JsonSerializerSettings Settings = new() {
			ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
			Formatting = Newtonsoft.Json.Formatting.Indented
		};
		
		//This is so ReSharper stops telling me that there are spelling errors, optional.
		[SuppressMessage("ReSharper", "StringLiteralTypo")] 
		private static readonly string[] DndBookNames = new[] {"ai", "bgdia", "cos", "dc", "dip", "dmg", "egw", "erlw", "esk", "ggr", "gos", "hftt", "hotdq", "idrotf", "imr", "kkw", "llk", "lmop", "lr", "mag", "mff", "mm", "mot", "mtf", "oota", "oow", "phb", "pota", "ps-a", "ps-d", "ps-i", "ps-x", "ps-k", "ps-z", "rmbre", "rot", "sads", "sdw", "skt", "slw", "tce", "tftyp", "toa", "ttp", "ua-2020smt", "ua-20s2", "ua-20s5", "ua-ar", "ua-cfv", "ua-cdw", "vgm", "xge", "wdh", "wdmm" };
		private static readonly string ApiVersion = "1.125.0";
		private static readonly List<dynamic> RawApiMonsters = new();
		public static readonly Dictionary<string, int> CRToXP = new() {
			{"0", 10},
			{"1/8", 25},
			{"1/4", 50},
			{"1/2", 100},
			{"1", 200},
			{"2", 450},
			{"3", 700},
			{"4", 1100},
			{"5", 1800},
			{"6", 2300},
			{"7", 2900},
			{"8", 3900},
			{"9", 5000},
			{"10", 5900},
			{"11", 7200},
			{"12", 8400},
			{"13", 10000},
			{"14", 11500},
			{"15", 13000},
			{"16", 15000},
			{"17", 18000},
			{"19", 22000},
			{"20", 25000},
			{"21", 33000},
			{"22", 41000},
			{"23", 50000},
			{"24", 62000},
			{"25", 75000},
			{"26", 90000},
			{"27", 105000},
			{"28", 120000},
			{"29", 135000},
			{"30", 155000}
		};
		
		//Recursive Enumerator for HtmlNode
		internal static IEnumerable<HtmlNode> RecursiveEnumerator(this HtmlNode node) {
			Queue<HtmlNode> nodeQueue = new(node.ChildNodes);
			while (nodeQueue.TryDequeue(out HtmlNode? current)) {
				yield return current;
				foreach (var childNode in current.ChildNodes) {
					nodeQueue.Enqueue(childNode);
				}
			}
		}
		//GET request for website content, parsed with HTML Agility Pack
		internal static async Task<string> HttpGet(string URL) {
			using var client = new HttpClient();
			using var request = new HttpRequestMessage {RequestUri = new Uri(URL), Method = HttpMethod.Get};
			using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);
			if (response.StatusCode == HttpStatusCode.NotFound) return "404";
			return await response.Content.ReadAsStringAsync();
		}
		
		//Xml Serializer
		internal static void Serialize<T>(T serializableObject, string fileName) {
			
			if (serializableObject == null) return;
			try {
				XmlDocument document = new();
				XmlSerializer serializer = new(typeof(T));
				using MemoryStream memStream = new();
				serializer.Serialize(memStream, serializableObject);
				memStream.Position = 0;
				document.Load(memStream);
				document.Save(fileName);
			}
			catch (Exception e) {
				Console.WriteLine(e.Message);
			}
		}

		internal static T Deserialize<T>(string fileName) {

			if (string.IsNullOrEmpty(fileName)) return default!;
			T objectOutput = default;

			try {
				XmlDocument document = new();
				document.Load(fileName);
				string outerXml = document.OuterXml;

				using StringReader reader = new(outerXml);
				XmlSerializer serializer = new(typeof(T));
				using XmlReader xmlReader = new XmlTextReader(reader);
				objectOutput = (T) serializer.Deserialize(xmlReader)!;
			}
			catch (Exception e) {
				Console.WriteLine(e.Message);
			}
			return objectOutput!;
		}
		

		//Moved embed conversion to HelperFunctions.cs and extracted it to method 
		internal static EmbedBuilder ToEmbed(this IEnumerable<string> text, EmbedBuilder builder = null!) {
			string currentField = string.Empty;
			bool firstEmbedBlock = true;
			bool arraySequence = false;

			foreach (string chunk in text) {
				
				if (chunk.Length < ArrayDetectionThreshold) {
					if (arraySequence) {
						currentField += " " + chunk;
						arraySequence = false;
					}
					else arraySequence = true;
				}

				if (arraySequence) currentField += "\n\n**" + chunk + (chunk.Last() == '.' ? "" : " - ") + "**"; //Insecure in case of array overflow (1024 characters) - will tackle if problem
				else if (chunk.Length + currentField.Length < 1022 && chunk.Length >= ArrayDetectionThreshold) currentField += "\n\n" + chunk;
				else if (chunk.Length >= ArrayDetectionThreshold) {
					builder.AddField(firstEmbedBlock ? "Description" : " ", currentField);
					currentField = chunk;
					firstEmbedBlock = false;
				}
			}

			if (currentField != string.Empty) builder.AddField(firstEmbedBlock ? "Description" : " ", currentField);
			return builder;
		}

		private static async Task LoadMonsters() {
			foreach (string bookName in DndBookNames) {
				var httpResponse = await HttpGet($"https://5e.tools/data/bestiary/bestiary-{bookName}.json?v={ApiVersion}");
				if (httpResponse == "404") throw new ArgumentException(bookName);
				var tempDynamic = ((JArray) ((dynamic) JsonConvert.DeserializeObject(httpResponse)).monster).Cast<dynamic>();
				RawApiMonsters.AddRange(tempDynamic);
			}
		}

		public static async Task<dynamic> QueryMonsterByName(string nameFragment) {
			
			//Checks for cache before sending GET request / iterating 2000 element array
			
			string formattedName = nameFragment.ToLower().Replace(" ", "-").Replace("'", "");
			if (File.Exists($"./cache/monsters/{formattedName}.json")) return JsonConvert.DeserializeObject(await File.ReadAllTextAsync($"./cache/monsters/{formattedName}.json"), Settings);
			
			//Does standard check for name. Returns 0 if nothing found
			
			if (RawApiMonsters.Count == 0) await LoadMonsters();
			dynamic result = RawApiMonsters.Select(d => { //Prioritizes exact match over partial
				bool yes = ((string) d.name).ToLower().Contains(nameFragment.ToLower());
				int resultScore = yes ? ((string) d.name).ToLower() == nameFragment.ToLower() ? 2 : 1 : 0;
				return new KeyValuePair<dynamic, int>(d, resultScore);
			}).MaxReference(k => k.Value).Key;
			if (!(result is int intResult && intResult == 0)) {
				formattedName = ((string) result.name).ToLower().Replace(" ", "-").Replace("'", "");
				await File.WriteAllTextAsync($"./cache/monsters/{formattedName}.json",JsonConvert.SerializeObject(result, Settings));
			}
			return result;
		}

		public static async Task<string> GetMonsterImageUrl(dynamic monster) {
			string url = $"https://5e.tools/img/{monster.source}/{((string) monster.name).Replace(" ", "%20")}.png?v={ApiVersion}";
			if (await HttpGet(url) == "404") return "404";
			return url;
		}

		public static string GetMonsterSizeRaceAndAlignment(dynamic monster) {
			string result = (string) GetSize(monster) + " ";
			JToken jsonObject = (JToken) monster.type;
			if (jsonObject.Type == JTokenType.String) result += ((string) jsonObject).CapitalizeFirst();
			else {
				result += ((string) monster.type.type).CapitalizeFirst();
				string tags = " (";
				tags += string.Join(", ", ((JArray) monster.type.tags).Select(j => j.Value<string>().CapitalizeFirst())) + ")";
				result += tags;
			}

			result += ", ";
			string[] alignments = ((JArray) monster.alignment).Select(j => j.Value<string>()).ToArray();
			if (alignments.Length > 2) {
				string mix = string.Join("", alignments);
				char[] values = new[] {'G', 'E', 'L', 'C', 'N'};
				foreach (char c in values) {
					if (!mix.Contains(c)) {
						result += $"Any Non-{GetAlignment(c)} Alignment";
						break;
					}
				}
			}
			else {
				result += string.Join(" ", alignments.Select(s => GetAlignment(s[0])));
			}
			return result;
		}

		private static string GetSize(dynamic monster) =>
			(string) monster.size switch {
				"T" => "Tiny",
				"S" => "Small",
				"M" => "Medium",
				"L" => "Large",
				"H" => "Huge",
				"G" => "Gargantuan",
				_ => throw new ArgumentOutOfRangeException()
			};

		private static string GetAlignment(char c) => c switch {
			'N' => "Neutral",
			'L' => "Lawful",
			'G' => "Good",
			'C' => "Chaotic",
			'E' => "Evil",
			'A' => "Any",
			'U' => "Unaligned",
			_ => throw new ArgumentOutOfRangeException(nameof(c), c, null)
		};

		private static string CapitalizeFirst(this string str) => char.ToUpper(str[0]) + str[1..];
		public static int CalculateBonus(this int score) => (score - 10) / 2;

		//Max function but returns source object instead of result
		private static T MaxReference<T>(this IEnumerable<T> enumerable, Func<T, int> action) {
			var items = enumerable as T[] ?? enumerable.ToArray();
			T current = items.First();
			int value = action(current);
			foreach (T item in items) {
				int tValue = action(item);
				if (tValue > value) {
					current = item;
					value = tValue;
				}
			}

			return current;
		}
	}
}