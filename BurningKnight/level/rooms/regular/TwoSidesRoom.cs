using BurningKnight.entity.room.controllable.platform;
using BurningKnight.level.tile;
using BurningKnight.util.geometry;
using Lens.util.math;
using Microsoft.Xna.Framework;

namespace BurningKnight.level.rooms.regular {
	public class TwoSidesRoom : RegularRoom {
		private Rect rect;
		private bool vertical;

		public TwoSidesRoom() {
			vertical = Random.Chance();
		}

		public override void Paint(Level level) {
			SetupRect();
			Painter.Fill(level, rect, Tile.Chasm);

			var platform = new MovingPlatform();

			platform.X = vertical ? (Random.Int(Left + 1, Right - 1)) * 16 : (Left + GetWidth() / 2) * 16;
			platform.Y = vertical ? (Top + GetHeight() / 2) * 16 : (Random.Int(Top + 1, Bottom - 1)) * 16;
			platform.Direction = new Vector2(vertical ? 0 : 1, vertical ? 1 : 0);

			level.Area.Add(platform);
		}

		public override bool CanConnect(Vector2 P) {
			if (vertical) {
				if ((int) P.X == Left || (int) P.X == Right) {
					return false;
				}	
			} else {
				if ((int) P.Y == Top || (int) P.Y == Bottom) {
					return false;
				}	
			}
			
			return base.CanConnect(P);
		}

		private void SetupRect() {
			if (rect != null) {
				return;
			}
			
			rect = new Rect();
			
			if (!vertical) {
				rect.Top = Top + 1;
				rect.Bottom = Bottom;

				rect.Left = Left + 3 + Random.Int(3);
				rect.Right = Right - 2 - Random.Int(3);
			} else {
				rect.Left = Left + 1;
				rect.Right = Right;
				
				rect.Top = Top + 3 + Random.Int(3);
				rect.Bottom = Bottom - 2 - Random.Int(3);
			}
		}
		
		public override int GetMinWidth() {
			return vertical ? 8 : 12;
		}

		public override int GetMinHeight() {
			return vertical ? 12 : 8;
		}

		public override int GetMaxWidth() {
			return vertical ? 12 : 18;
		}

		public override int GetMaxHeight() {
			return vertical ? 12 : 18;
		}
	}
}