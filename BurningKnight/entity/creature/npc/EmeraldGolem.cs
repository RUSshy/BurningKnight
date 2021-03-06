using BurningKnight.assets;
using BurningKnight.assets.items;
using BurningKnight.assets.particle;
using BurningKnight.entity.component;
using BurningKnight.entity.creature.player;
using BurningKnight.entity.events;
using BurningKnight.entity.item;
using BurningKnight.save;
using BurningKnight.state;
using BurningKnight.ui.dialog;
using BurningKnight.util;
using Lens;
using Lens.assets;
using Lens.entity;
using Lens.util.camera;
using Lens.util.file;
using Lens.util.math;
using Lens.util.timer;
using VelcroPhysics.Dynamics;

namespace BurningKnight.entity.creature.npc {
	public class EmeraldGolem : Npc {
		private const int Amount = 20;
		private bool broken;
		
		public override void AddComponents() {
			base.AddComponents();
			
			Width = 31;
			Height = 33;
			
			AddComponent(new AnimationComponent("emerald_golem"));
			AddComponent(new SensorBodyComponent(-Npc.Padding, -Npc.Padding, Width + Npc.Padding * 2, Height + Npc.Padding, BodyType.Static));
			AddComponent(new RectBodyComponent(2, 17, 27, 16, BodyType.Static));
			
			AddComponent(new InteractableComponent(Interact) {
				CanInteract = e => !broken
			});
			
			GetComponent<DialogComponent>().Dialog.Voice = 15;
			
			Dialogs.RegisterCallback("eg_0", (d, c) => {
				if (broken) {
					return null;
				}
			
				if (((ChoiceDialog) d).Choice == 0) {
					Timer.Add(() => {
						GetComponent<DialogComponent>().StartAndClose(Locale.Get("eg_1"), 3);
					}, 0.2f);

					var inv = c.To.GetComponent<InventoryComponent>();
					var a = c.To.Area;
			
					for (var i = 0; i < Amount; i++) {
						inv.Pickup(Items.CreateAndAdd("bk:emerald", a));
					}

					Timer.Add(() => {
						inv.Pickup(Items.CreateAndAdd(Scourge.GenerateItemId(), a));
					}, 1f);

					Timer.Add(() => {
						GetComponent<AnimationComponent>().Animate(() => {
							Done = true;
							Engine.Instance.Flash = 1f;
							Camera.Instance.Shake(8);
							
							for (var i = 0; i < 4; i++) {
								var part = new ParticleEntity(Particles.Dust());
						
								part.Position = Center + Rnd.Vector(-16, 16);
								part.Particle.Scale = Rnd.Float(1f, 2f);
								Run.Level.Area.Add(part);
								part.Depth = 1;
							}
						});
					}, 4f);

					broken = true;
					return null;
				} else if (GlobalSave.IsTrue("bk:emerald_gun")) {
					Timer.Add(() => {
						GetComponent<DialogComponent>().StartAndClose(Locale.Get("eg_1"), 3);
					}, 0.2f);

					var inv = c.To.GetComponent<InventoryComponent>();
					var a = c.To.Area;
			
					inv.Pickup(Items.CreateAndAdd("bk:emerald_gun", a));

					Timer.Add(() => {
						inv.Pickup(Items.CreateAndAdd(Scourge.GenerateItemId(), a));
					}, 1f);

					Timer.Add(() => {
						GetComponent<AnimationComponent>().Animate(() => {
							Done = true;
							Engine.Instance.Flash = 1f;
							Camera.Instance.Shake(8);
							
							for (var i = 0; i < 4; i++) {
								var part = new ParticleEntity(Particles.Dust());
						
								part.Position = Center + Rnd.Vector(-16, 16);
								part.Particle.Scale = Rnd.Float(1f, 2f);
								Run.Level.Area.Add(part);
								part.Depth = 1;
							}
						});
					}, 4f);

					broken = true;
					return null;
				}

				return null;
			});
			
			Subscribe<RoomChangedEvent>();
		}

		public override bool HandleEvent(Event e) {
			if (e is RoomChangedEvent rce && rce.Who is Player) {
				if (rce.New == GetComponent<RoomComponent>().Room) {
					GetComponent<DialogComponent>().StartAndClose("eg_3", 3);
				} else {
					GetComponent<DialogComponent>().Close();
				}
			}
			
			return base.HandleEvent(e);
		}

		private bool Interact(Entity e) {
			GetComponent<DialogComponent>().Start("eg_0", e);
			return true;
		}

		public override void Load(FileReader stream) {
			base.Load(stream);
			broken = stream.ReadBoolean();
			// todo: update the sprite
		}

		public override void Save(FileWriter stream) {
			base.Save(stream);
			stream.WriteBoolean(broken);
		}

		public override bool ShouldCollide(Entity entity) {
			return true;
		}
	}
}