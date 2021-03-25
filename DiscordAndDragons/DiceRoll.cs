using System;
using System.Linq;
using System.Security.Cryptography;

namespace DiscordAndDragons {
	public struct DiceRoll {

		public int Multiplier;
		public int DiceValue;
		public int Constant;
		public bool Advantage;
		public bool Disadvantage;
		public bool WithAverage;


		public int Evaluate() {
			if (Multiplier != 0) {
				if (WithAverage) return (DiceValue / 2 + 1) * Multiplier; // Average formula. Derived from Sum(1 to DiceValue) / DiceValue
				
				//Need to derive fields, can't access struct fields in lambda
				
				int diceValue = DiceValue;
				bool advantage = Advantage;
				
				//Using Math.Abs due to negative multiplier
				
				int[] rolls = new int[Math.Abs(Multiplier)].Select(_ => RandomNumberGenerator.GetInt32(1, diceValue+1)).ToArray(); //Crypto-safe RNG
				if (Advantage || Disadvantage) {
					int[] rolls2 = new int[Math.Abs(Multiplier)].Select(_ => RandomNumberGenerator.GetInt32(1, diceValue+1)).ToArray();
					rolls = rolls2.Zip(rolls, Tuple.Create).Select(t => advantage ? Math.Max(t.Item1, t.Item2) : Math.Min(t.Item1, t.Item2)).ToArray();
				}
				return rolls.Sum() * (Multiplier < 0 ? -1 : 1);
			}

			return Constant;
		}
	}
}