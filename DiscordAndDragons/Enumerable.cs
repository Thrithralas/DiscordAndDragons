using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;

namespace DiscordAndDragons {
	public static partial class Enumerable {
		public static IEnumerable<HtmlNode> RecursiveEnumerator(this HtmlNode node) {
			Queue<HtmlNode> nodeQueue = new Queue<HtmlNode>(node.ChildNodes);
			while (nodeQueue.TryDequeue(out HtmlNode current)) {
				yield return current;
				foreach (var childNode in current.ChildNodes) {
					nodeQueue.Enqueue(childNode);
				}
			}
		}
	}
}