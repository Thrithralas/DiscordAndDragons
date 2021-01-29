using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Discord;
using HtmlAgilityPack;

namespace DiscordAndDragons {
	public static class HelperFunctions {

		internal static readonly int ArrayDetectionThreshold = 10; //ArrayDetectionThreshold is now a static constant
		
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

		internal static IEnumerable<string> RemoveEmpty(this IEnumerable<string> enumerable) {
			return enumerable.Where(t => !string.IsNullOrEmpty(t));
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
	}
}