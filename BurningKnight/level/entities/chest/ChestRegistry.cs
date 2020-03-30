using System;
using BurningKnight.entity.pool;

namespace BurningKnight.level.entities.chest {
	public class ChestRegistry : Pool<Type> {
		public static ChestRegistry Instance = new ChestRegistry();

		static ChestRegistry() {
			Instance.Add(typeof(WoodenChest), 1f);
			Instance.Add(typeof(ScourgedChest), 0.9f);
			Instance.Add(typeof(DoubleChest), 0.1f);
			Instance.Add(typeof(TripleChest), 0.01f);
			Instance.Add(typeof(StoneChest), 1f);
			Instance.Add(typeof(GoldChest), 1f);
			Instance.Add(typeof(RedChest), 0.5f);
			Instance.Add(typeof(GlassChest), 0.5f);
		}
	}
}