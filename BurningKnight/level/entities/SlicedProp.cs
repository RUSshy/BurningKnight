using BurningKnight.entity.component;
using BurningKnight.save;
using BurningKnight.ui.editor;
using Lens.entity.component.graphics;
using Lens.util.file;

namespace BurningKnight.level.entities {
	public class SlicedProp : Prop {
		public string Sprite;
		
		public SlicedProp(string slice = null, int depth = 0) {
			Sprite = slice;
			Depth = depth;
		}

		// Used for loading
		public SlicedProp() {
			
		}
		
		public override void PostInit() {
			base.PostInit();
			InsertGraphics();
		}

		protected virtual GraphicsComponent CreateGraphicsComponent() {
			return HasComponent<InteractableComponent>()
					? new InteractableSliceComponent("props", Sprite)
					: new SliceComponent("props", Sprite);
		}

		protected virtual void InsertGraphics() {
			AddComponent(CreateGraphicsComponent());
		}

		public override void Save(FileWriter stream) {
			base.Save(stream);
			stream.WriteString(Sprite);
			stream.WriteSbyte((sbyte) Depth);
		}

		public override void Load(FileReader stream) {
			base.Load(stream);
			Sprite = stream.ReadString();
			Depth = stream.ReadSbyte();
		}
	}
}