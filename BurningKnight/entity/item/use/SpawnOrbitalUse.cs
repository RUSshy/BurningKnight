using BurningKnight.assets.items;
using BurningKnight.entity.component;
using BurningKnight.entity.orbital;
using BurningKnight.util;
using ImGuiNET;
using Lens.entity;
using Lens.lightJson;
using Lens.util;

namespace BurningKnight.entity.item.use {
	public class SpawnOrbitalUse : ItemUse {
		private string orbital;
		private bool random;

		public override void Use(Entity entity, Item item) {
			if (random) {
				entity.GetComponent<InventoryComponent>().Pickup(Items.CreateAndAdd(Items.Generate(ItemPool.Orbital), entity.Area, true));
				return;
			}
			
			var o = OrbitalRegistry.Create(orbital, entity);

			if (o == null) {
				Log.Error($"Failed to create orbital with id {orbital}");
				return;
			}
			
			entity.GetComponent<OrbitGiverComponent>().AddOrbiter(o);
		}

		public override void Setup(JsonValue settings) {
			base.Setup(settings);

			random = settings["random"].Bool(false);
			orbital = settings["orbital"].String("");
		}

		public static void RenderDebug(JsonValue root) {
			if (root.Checkbox("Random", "random", false)) {
				return;
			}
			
			if (!OrbitalRegistry.Has(root.InputText("Orbital", "orbital", "", 128))) {
				ImGui.BulletText("Unknown orbital!");
			}
		}
	}
}