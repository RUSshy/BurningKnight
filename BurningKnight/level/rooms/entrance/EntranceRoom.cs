using System;
using BurningKnight.assets.items;
using BurningKnight.entity.creature.npc;
using BurningKnight.entity.door;
using BurningKnight.entity.room.controllable.turret;
using BurningKnight.level.biome;
using BurningKnight.level.entities;
using BurningKnight.level.entities.decor;
using BurningKnight.level.tile;
using BurningKnight.level.walls;
using BurningKnight.save;
using BurningKnight.state;
using BurningKnight.util.geometry;
using Lens.entity;
using Lens.util;
using Lens.util.math;
using Microsoft.Xna.Framework;

namespace BurningKnight.level.rooms.entrance {
	public class EntranceRoom : RoomDef {
		public override int GetMinConnections(Connection Side) {
			if (Side == Connection.All) return 1;
			return 0;
		}

		public override int GetMaxConnections(Connection Side) {
			if (Side == Connection.All) return 16;
			return 4;
		}

		public override void Paint(Level level) {
			if (IceDemoRoom()) {
				Painter.Fill(level, this, Tile.WallA);
				Painter.FillEllipse(level, this, 3, Tiles.RandomFloor());
				
				level.Area.Add(new Turret {
					Position = GetTileCenter() * 16 + new Vector2(32, 0),
					StartingAngle = 0
				});
			} else {
				WallRegistry.Paint(level, this, EntranceWallPool.Instance);
			}
			
			var prop = new Entrance {
				To = Run.Depth + 1
			};

			var where = GetCenter();
			
			Painter.Fill(level, where.X - 1, where.Y - 1, 3, 3, Tiles.RandomFloor());

			level.Area.Add(prop);
			prop.Center = (where * 16 + new Vector2(8));

			MakeSafe(level);

			try {
				if (Builder.ShouldAppear()) {
					var b = new Builder();
					b.BottomCenter = where * 16 + new Vector2(8 + Rnd.Float(-16, 16), 20);
					level.Area.Add(b);
				}
			} catch (Exception e) {
				Log.Error(e);
			}
			
			if (Run.Type == RunType.BossRush && Run.Depth > 1) {
				var item = Items.CreateAndAdd("bk:battery", level.Area);
				
				item.Center = where * 16 + new Vector2(8 + Rnd.Float(-16, 16), Rnd.Float(-16, 16));
			}
		}

		protected void MakeSafe(Level level) {
			var t = Tiles.RandomFloor();
			
			Painter.Call(level, this, 1, (x, y) => {
				if (level.Get(x, y).Matches(Tile.SpikeOffTmp, Tile.SensingSpikeTmp, Tile.Chasm, Tile.Lava)) {
					level.Set(x, y, t);
				}
			});
			
			/*if (Run.Depth == Run.ContentEndDepth) {
				var om = new OldMan();
				level.Area.Add(om);
				om.Center = ((GetRandomDoorFreeCell() ?? GetTileCenter()) * 16 + new Dot(8)).ToVector();
			}*/
		}

		private static bool IceDemoRoom() {
			return Run.Type == RunType.Regular && LevelSave.BiomeGenerated is IceBiome && Run.Depth % 2 == 1;
		}

		public override int GetMinWidth() {
			return IceDemoRoom() ? 16 : 5;
		}

		public override int GetMinHeight() {
			return IceDemoRoom() ? 10 : 5;
		}

		public override int GetMaxWidth() {
			return IceDemoRoom() ? 17 : 13;
		}

		public override int GetMaxHeight() {
			return 13;
		}
	}
}