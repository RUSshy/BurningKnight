using BurningKnight.entity.component;
using BurningKnight.entity.creature.mob.prefabs;
using Lens.graphics;
using Microsoft.Xna.Framework;

namespace BurningKnight.entity.creature.mob.castle {
	public class SimpleSlime : Slime {
		private static readonly Color color = ColorUtils.FromHex("#33984b");
		
		protected override Color GetColor() {
			return color;
		}
		
		protected override void SetStats() {
			base.SetStats();
			
			AddComponent(new ZAnimationComponent("slime"));
			SetMaxHp(2);

			var body = new RectBodyComponent(2, 7, 12, 9);
			AddComponent(body);

			body.Body.LinearDamping = 2;
			body.KnockbackModifier = 0.5f;
		}
	}
}