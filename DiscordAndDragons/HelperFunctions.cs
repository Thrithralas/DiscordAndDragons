using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using HtmlAgilityPack;

namespace DiscordAndDragons {
	public static class HelperFunctions {
		
		//Recursive Enumerator for HtmlNode
		public static IEnumerable<HtmlNode> RecursiveEnumerator(this HtmlNode node) {
			Queue<HtmlNode> nodeQueue = new Queue<HtmlNode>(node.ChildNodes);
			while (nodeQueue.TryDequeue(out HtmlNode current)) {
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
				XmlDocument document = new XmlDocument();
				XmlSerializer serializer = new XmlSerializer(typeof(T));
				using MemoryStream memStream = new MemoryStream();
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

			if (string.IsNullOrEmpty(fileName)) return default;
			T objectOutput = default;

			try {
				XmlDocument document = new XmlDocument();
				document.Load(fileName);
				string outerXml = document.OuterXml;

				using StringReader reader = new StringReader(outerXml);
				XmlSerializer serializer = new XmlSerializer(typeof(T));
				using XmlReader xmlReader = new XmlTextReader(reader);
				objectOutput = (T) serializer.Deserialize(xmlReader);
			}
			catch (Exception e) {
				Console.WriteLine(e.Message);
			}
			return objectOutput;
		}
	}
}