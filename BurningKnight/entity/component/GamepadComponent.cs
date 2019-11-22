using System;
using Lens.entity.component;
using Lens.entity.component.logic;
using Lens.input;
using Lens.util;
using Lens.util.camera;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BurningKnight.entity.component {
	public class GamepadComponent : Component {
		public static GamepadData Current;
		
		public GamepadData Controller;

		public string GamepadId;

		static GamepadComponent() {
			Camera.OnShake += () => {
				if (Current == null || !Settings.Vibrate) {
					return;
				}

				var a = Math.Min(1, Camera.Instance.GetComponent<ShakeComponent>().Amount / 20f);
				Current.Rumble(a, a);
			};
		}
		
		public override void Update(float dt) {
			base.Update(dt);

			if (Settings.Gamepad != GamepadId && Settings.Gamepad != null) {
				for (int i = 0; i < 4; i++) {
					if (GamePad.GetCapabilities(i).IsConnected && GamePad.GetCapabilities(i).Identifier == Settings.Gamepad) {
						Controller = Input.Gamepads[i];
						GamepadId = Settings.Gamepad;
						Current = Controller;
						
						Log.Info($"Connected {GamePad.GetState(i)}");
						break;
					}
				}
				
				Log.Error($"Unknown gamepad ${Settings.Gamepad}");
				Current = null;
				Settings.Gamepad = null;
			} else if (Controller == null) {
				for (int i = 0; i < 4; i++) {
					if (Input.Gamepads[i].Attached) {
						Controller = Input.Gamepads[i];
						GamepadId = GamePad.GetCapabilities(i).Identifier;
						Current = Controller;
						
						Settings.Gamepad = GamepadId;
						Log.Info($"Connected {GamePad.GetState(i)}");
						break;
					}
				}
			}
		}
	}
}