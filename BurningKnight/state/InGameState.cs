﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BurningKnight.assets;
using BurningKnight.assets.achievements;
using BurningKnight.assets.input;
using BurningKnight.assets.lighting;
using BurningKnight.assets.particle.custom;
using BurningKnight.entity;
using BurningKnight.entity.component;
using BurningKnight.entity.creature.mob;
using BurningKnight.entity.creature.npc;
using BurningKnight.entity.creature.player;
using BurningKnight.entity.events;
using BurningKnight.entity.fx;
using BurningKnight.entity.item.stand;
using BurningKnight.entity.item.use;
using BurningKnight.entity.room;
using BurningKnight.level;
using BurningKnight.level.biome;
using BurningKnight.level.paintings;
using BurningKnight.level.rooms;
using BurningKnight.level.tile;
using BurningKnight.physics;
using BurningKnight.save;
using BurningKnight.save.statistics;
using BurningKnight.ui;
using BurningKnight.ui.dialog;
using BurningKnight.ui.editor;
using BurningKnight.ui.imgui;
using BurningKnight.ui.inventory;
using BurningKnight.util;
using ImGuiNET;
using Lens;
using Lens.assets;
using Lens.entity;
using Lens.entity.component.logic;
using Lens.game;
using Lens.graphics;
using Lens.graphics.gamerenderer;
using Lens.input;
using Lens.util;
using Lens.util.camera;
using Lens.util.tween;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Steamworks;
using Console = BurningKnight.debug.Console;
using Timer = Lens.util.timer.Timer;

namespace BurningKnight.state {
	public class InGameState : GameState, Subscriber {
		public static bool SkipPause;
		
		private const float AutoSaveInterval = 60f;
		private const float PaneTransitionTime = 0.2f;
		private const float BarsSize = 50;
		private static float TitleY = BarsSize / 2f;
		private static float BackY = Display.UiHeight - BarsSize / 2f;

		private bool pausedByMouseOut;
		private bool pausedByLostFocus;
		private float blur;
		private static TextureRegion fog;
		
		private UiPane pauseMenu;
		private UiPane gameOverMenu;

		private UiPane audioSettings;
		private UiPane graphicsSettings;
		private UiPane gameSettings;
		private UiPane confirmationPane;
		private UiPane inputSettings;
		private UiPane gamepadSettings;
		private UiPane keyboardSettings;
		private UiLabel killedLabel;

		private bool died;
		private Cursor cursor;
		private float saveTimer;
		private SaveIndicator indicator;
		private SaveLock saveLock = new SaveLock();

		private Painting painting;
		private EditorWindow editor;

		public bool Menu;
		public Area TopUi;

		private float vx;
		private string v;
		private float offset;
		private bool menuExited;
		private float blackBarsSize;
		private bool doneAnimatingPause = true;
		
		private TextureRegion gardient;
		private TextureRegion black;
		private TextureRegion emerald;

		public UiAnimation Killer;
		private Console console;
		private UiLabel seedLabel;
		private UiButton currentBack;
		private UiButton inputBack;
		private UiButton gamepadBack;
		private UiButton keyboardBack;

		public static bool Ready;
		public static bool InMenu;
		
		public void TransitionToBlack(Vector2 position, Action callback = null) {
			Camera.Instance.Targets.Clear();
			var v = Camera.Instance.CameraToScreen(position);

			Shaders.Ui.Parameters["bx"].SetValue(v.X / Display.UiWidth);
			Shaders.Ui.Parameters["by"].SetValue(v.Y / Display.UiHeight);

			Tween.To(0, 1, x => Shaders.Ui.Parameters["black"].SetValue(x), 0.7f).OnEnd = callback;

			Audio.FadeOut();
			Ready = false;
		}

		public void TransitionToOpen(Action callback = null) {
			Shaders.Ui.Parameters["bx"].SetValue(0.333f);
			Shaders.Ui.Parameters["by"].SetValue(0.333f);

			Tween.To(1, 0, x => Shaders.Ui.Parameters["black"].SetValue(x), 0.7f, Ease.QuadIn).OnEnd = () => {
				Ready = true;
				callback?.Invoke();
			};
		}

		public Painting CurrentPainting {
			set {
				painting = value;
				Paused = painting != null;
			}

			get => painting;
		}

		public InGameState(Area area, bool menu) {
			Menu = menu;
			InMenu = menu;
			Ready = false;
			Input.EnableImGuiFocus = false;

			Area = area;
			Area.EventListener.Subscribe<ItemCheckEvent>(this);
			Area.EventListener.Subscribe<DiedEvent>(this);
			Area.EventListener.Subscribe<GiveEmeraldsUse.GaveEvent>(this);

			black = CommonAse.Ui.GetSlice("black");
			emerald = CommonAse.Items.GetSlice("bk:emerald");

			if (Menu) {
				Input.Blocked = 1;

				blackBarsSize = BarsSize;
				gardient = CommonAse.Ui.GetSlice("gardient");
				blur = 1;

				offset = Display.UiHeight;
				Mouse.SetPosition((int) BK.Instance.GetScreenWidth() / 2, (int) BK.Instance.GetScreenHeight() / 2);

				Timer.Add(() => {
					Tween.To(0, offset, x => offset = x, 2f, Ease.BackOut);
					Audio.PlayMusic("Menu", true);
				}, 1f);
			} else {
				offset = Display.UiHeight;
			}
		}

		public override void Init() {
			base.Init();

			TopUi = new Area();

			Audio.Speed = 1f;

			try {
				Audio.Preload(((Biome) Activator.CreateInstance(BiomeRegistry.GenerateForDepth(Run.Depth + 1).Type)).Music);
			} catch (Exception e) {
				Log.Error(e);
			}
			
			Engine.Graphics.SynchronizeWithVerticalRetrace = Settings.Vsync;
			Engine.Graphics.ApplyChanges();

			Engine.Instance.StateRenderer.UiEffect = Shaders.Ui;
			
			if (Settings.Fullscreen && !Engine.Graphics.IsFullScreen) {
				Engine.Instance.SetFullscreen();
			}

			v = BK.Version.ToString();
			vx = -Font.Small.MeasureString(v).Width;

			Shaders.Ui.Parameters["black"].SetValue(Menu ? 1f : 0f);
			
			SetupUi();

			if (Run.Level?.Biome is CastleBiome) {
				for (var i = 0; i < 30; i++) {
					Area.Add(new WindFx());
				}
			}

			fog = Textures.Get("noise");
			Area.Add(new InGameAudio());

			foreach (var p in Area.Tagged[Tags.Player]) {
				if (p is LocalPlayer) {
					Camera.Instance.Follow(p, 1f, true);
					AreaDebug.ToFocus = p;
				}

				((Player) p).FindSpawnPoint();
			}

			if (!Menu) {
				Camera.Instance.Follow(cursor, CursorPriority);
			}

			Camera.Instance.Jump();
			
			if (Run.Depth == 0) {
				if (Weather.IsNight) {
					wasNight = true;
					var x = 0.25f;
					Lights.ClearColor = new Color(x, x, x, 1f);
				}

				if (Weather.Rains || Weather.Snows) {
					SetupParticles();
				}
			}

			if (!Menu) {
				TransitionToOpen();
			}

			FireParticle.Hook(Area);
			Run.StartedNew = false;
			
			if (Run.Depth > 0 && GameSave.IsFalse($"reached_{Run.Depth}")) {
				GameSave.Put($"reached_{Run.Depth}", true);
				Area.EventListener.Handle(new NewFloorEvent {
					WasInEL = true
				});
			}
			
			Run.Level.Prepare();

			if (Run.Depth == 0) {
				HatStand.CheckHats();
			}
		}

		private const float CursorPriority = 0.5f;

		public void ResetFollowing() {
			Camera.Instance.Targets.Clear();

			foreach (var p in Area.Tagged[Tags.Player]) {
				if (p is LocalPlayer) {
					Camera.Instance.Follow(p, 1f, true);
				}
			}

			Camera.Instance.Follow(cursor, CursorPriority);
		}

		public override void Destroy() {
			if (Engine.Quiting) {
				Run.SavingDepth = Run.Depth;
			}

			
			TopUi.Destroy();
			
			Timer.Clear();
			Lights.Destroy();

			Tween.To(1f, Audio.Speed, x => Audio.Speed = x, 1f);
			
			SaveManager.Backup();

			var old = !Engine.Quiting;

			SaveManager.Save(Area, SaveType.Global, old);
			// SaveManager.Save(Area, SaveType.Secret);

			if (!Run.StartedNew && !died && !Run.Won) {
				var d = (old ? Run.LastDepth : Run.Depth);
				
				if (d > 0) {
					SaveManager.Save(Area, SaveType.Level, old);
					SaveManager.Save(Area, SaveType.Player, old);
					SaveManager.Save(Area, SaveType.Game, old);
				}
			}

			Shaders.Screen.Parameters["split"].SetValue(0f);
			Shaders.Screen.Parameters["blur"].SetValue(0f);

			Area.Destroy();
			Area = null;

			Physics.Destroy();
			base.Destroy();
		}

		private float speedBeforePause;

		protected override void OnPause() {
			base.OnPause();

			if (died || InMenu || Run.Won) {
				return;
			}

			if (seedLabel != null) {
				seedLabel.Label = $"Seed: {Run.Seed}";
			}

			if (Settings.UiSfx) {
				Audio.PlaySfx("ui_goback", 0.5f);
			}

			Tween.To(this, new {blur = 1}, 0.25f);

			if (painting == null) {
				doneAnimatingPause = false;

				pauseMenu.X = 0;
				pauseMenu.Enabled = true;

				Tween.To(0, pauseMenu.Y, x => pauseMenu.Y = x, 0.5f, Ease.BackOut).OnEnd = () => {
					doneAnimatingPause = true;
					SelectFirst();
				};
			}

			speedBeforePause = Audio.Speed;

			Tween.To(0.5f, Audio.Speed, x => Audio.Speed = x, 1f);
			OpenBlackBars();
		}

		public void OpenBlackBars() {
			Tween.To(BarsSize, blackBarsSize, x => blackBarsSize = x, 0.3f);
		}
		
		public void CloseBlackBars() {
			Tween.To(0, blackBarsSize, x => blackBarsSize = x, 0.2f);
		}

		protected override void OnResume() {
			if (painting != null) {
				return;
			}

			base.OnResume();

			if (died || InMenu || Run.Won) {
				return;
			}

			doneAnimatingPause = false;

			Tween.To(this, new {blur = 0}, 0.25f);
			Tween.To(-Display.UiHeight, pauseMenu.Y, x => pauseMenu.Y = x, 0.25f).OnEnd = () => {
				pauseMenu.Enabled = false;
				doneAnimatingPause = true;
			};

			CloseBlackBars();
			Tween.To(speedBeforePause, Audio.Speed, x => Audio.Speed = x, 0.4f);

			pausedByMouseOut = false;
		}

		public override void OnActivated() {
			base.OnActivated();

			/*if (Paused && pausedByLostFocus && painting == null) {
				Paused = false;
			}*/
		}

		public override void OnDeactivated() {
			base.OnDeactivated();

			if (DialogComponent.Talking != null || !Settings.Autopause || !menuExited) {
				return;
			}

			Paused = true;
			pausedByLostFocus = true;
			pausedByMouseOut = false;
		}

		private void SelectFirst() {
			SelectFirst(false);
		}

		private void SelectFirst(bool force) {
			if (!force && GamepadComponent.Current == null) {
				return;
			}
		
			var min = UiButton.LastId;
			UiButton btn = null;

			foreach (var b in Ui.Tagged[Tags.Button]) {
				var bt = ((UiButton) b);

				if (bt.Active && bt.IsOnScreen() && bt.Id < min) {
					btn = bt;
					min = bt.Id;
				}
			}

			if (btn != null) {
				UiButton.SelectedInstance = btn;
				UiButton.Selected = btn.Id;
			}
		}

		private Vector2 stickOffset;
		private bool wasNight;
		private bool wasRaining;
		private List<Entity> particles = new List<Entity>();

		private void SetupParticles() {
			if (Weather.Rains) {
				for (var i = 0; i < 40; i++) {
					particles.Add(Run.Level.Area.Add(new RainParticle {
						Custom = true
					}));
				}
			} else if (Weather.Snows) {
				for (var i = 0; i < 100; i++) {
					particles.Add(Run.Level.Area.Add(new SnowParticle {
						Custom = true
					}));
				}
			}
		}
		
		public override void Update(float dt) {
			if (UiAchievement.Current == null) {
				if (Achievements.AchievementBuffer.Count > 0) {
					var id = Achievements.AchievementBuffer[0];
				
					var a = new UiAchievement(id);
					a.Y = Display.UiHeight + 60;
					TopUi.Add(a);
					a.Right = Display.UiWidth - 8;
				} else if (Achievements.ItemBuffer.Count > 0) {
					var id = Achievements.ItemBuffer[0];
				
					var a = new UiAchievement(id, true);
					a.Y = Display.UiHeight + 60;
					TopUi.Add(a);
					a.Right = Display.UiWidth - 8;
				}
			}
			
			if (!Paused && (Settings.Autosave && Run.Depth > 0)) {
				if (!saving) {
					saveTimer += dt;

					if (saveTimer >= AutoSaveInterval) {
						saveTimer = 0;
						saving = true;
						saveLock.Reset();

						indicator.HandleEvent(new SaveStartedEvent());

						new Thread(() => {
							try {
								SaveManager.Backup();

								SaveManager.ThreadSave(saveLock.UnlockGlobal, Area, SaveType.Global);
								SaveManager.ThreadSave(saveLock.UnlockGame, Area, SaveType.Game);

								SaveManager.ThreadSave(saveLock.UnlockLevel, Area, SaveType.Level);
								SaveManager.ThreadSave(saveLock.UnlockPlayer, Area, SaveType.Player);
							} catch (Exception e) {
								Log.Error(e);
							}
						}) {
							Priority = ThreadPriority.Lowest
						}.Start();
					}
				} else if (saveLock.Done) {
					saving = false;
					indicator.HandleEvent(new SaveEndedEvent());
				}
			}

			if (Paused || died || Run.Won) {
				if (UiButton.SelectedInstance != null && (!UiButton.SelectedInstance.Active || !UiButton.SelectedInstance.IsOnScreen())) {
					UiButton.SelectedInstance = null;
					UiButton.Selected = -1;
				}

				var gamepad = GamepadComponent.Current;
				
				if (UiButton.SelectedInstance == null && (Input.WasPressed(Controls.UiDown, gamepad, true) || Input.WasPressed(Controls.UiUp, gamepad, true))) {
					SelectFirst(true);

					if (Settings.UiSfx) {
						Audio.PlaySfx("ui_moving", 0.5f);
					}
				} else if (UiButton.Selected > -1) {
					if (Input.WasPressed(Controls.UiDown, gamepad, true)) {
						UiButton sm = null;
						var mn = UiButton.LastId;
						
						foreach (var b in Ui.Tagged[Tags.Button]) {
							var bt = ((UiButton) b);

							if (bt.Active && bt.IsOnScreen() && bt.Id > UiButton.Selected && bt.Id < mn) {
								mn = bt.Id;
								sm = bt;
							}
						}

						if (sm != null) {
							UiButton.SelectedInstance = sm;
							UiButton.Selected = sm.Id;

							if (Settings.UiSfx) {
								Audio.PlaySfx("ui_moving", 0.5f);
							}
						} else {
							var min = UiButton.Selected;
							UiButton btn = null;
							
							foreach (var b in Ui.Tagged[Tags.Button]) {
								var bt = ((UiButton) b);

								if (bt.Active && bt.IsOnScreen() && bt.Id < min) {
									btn = bt;
									min = bt.Id;
								}
							}

							if (btn != null) {
								UiButton.SelectedInstance = btn;
								UiButton.Selected = btn.Id;

								if (Settings.UiSfx) {
									Audio.PlaySfx("ui_moving", 0.5f);
								}
							}
						}
					} else if (Input.WasPressed(Controls.UiUp, gamepad, true)) {
						UiButton sm = null;
						var mn = -1;
						
						foreach (var b in Ui.Tagged[Tags.Button]) {
							var bt = ((UiButton) b);

							if (bt.Active && bt.IsOnScreen() && bt.Id < UiButton.Selected && bt.Id > mn) {
								mn = bt.Id;
								sm = bt;
							}
						}

						if (sm != null) {
							UiButton.SelectedInstance = sm;
							UiButton.Selected = sm.Id;

							if (Settings.UiSfx) {
								Audio.PlaySfx("ui_moving", 0.5f);
							}
						} else {
							var max = -1;
							UiButton btn = null;
							
							foreach (var b in Ui.Tagged[Tags.Button]) {
								var bt = ((UiButton) b);

								if (bt.Active && bt.IsOnScreen() && bt.Id > max) {
									btn = bt;
									max = bt.Id;
								}
							}

							if (btn != null) {
								UiButton.SelectedInstance = btn;
								UiButton.Selected = btn.Id;

								if (Settings.UiSfx) {
									Audio.PlaySfx("ui_moving", 0.5f);
								}
							}
						}
					}
				}
			}

			if (!Paused) {
				Weather.Update(dt);

				if (Run.Depth == 0) {
					var night = Weather.IsNight;

					if (night != wasNight) {
						wasNight = night;
						var v = night ? 0.25f : 0.9f;

						Tween.To(v, Lights.ClearColor.R / 255f, x => { Lights.ClearColor = new Color(x, x, x, 1f); }, 10f);
					}

					var raining = Weather.Rains || Weather.Snows;

					if (wasRaining != raining) {
						wasRaining = raining;

						if (raining) {
							SetupParticles();
						} else {
							foreach (var p in particles) {
								if (p is RainParticle r) {
									r.End = true;
								} else if (p is SnowParticle s) {
									s.End = true;
								} else {
									p.Done = true;
								}
							}

							particles.Clear();
						}
					}
				}
			}
			
			var inside = Engine.GraphicsDevice.Viewport.Bounds.Contains(Input.Mouse.CurrentState.Position);
			
			Shaders.Screen.Parameters["split"].SetValue(Engine.Instance.Split);
			Shaders.Screen.Parameters["blur"].SetValue(blur);

			if (DialogComponent.Talking == null) {
				if (!Paused && !inside && !Engine.Version.Test && Settings.Autopause) {
					Paused = true;
					pausedByMouseOut = true;
				} else if (Paused && pausedByMouseOut && inside) {
					Paused = false;
				}
			}

			if (Menu && !menuExited) {
				if (Input.WasPressed(Controls.GameStart, GamepadComponent.Current, true) || Input.Keyboard.State.GetPressedKeys().Length > 0) {
					menuExited = true;
					InMenu = false;
					Input.Blocked = 0;

					Audio.PlaySfx("ui_start");
					Audio.PlayMusic("Hub", true);

					CloseBlackBars();
					Tween.To(this, new {blur = 0}, 0.5f).OnEnd = () => Camera.Instance.Follow(cursor, CursorPriority);
					Tween.To(-Display.UiHeight, offset, x => offset = x, 0.5f, Ease.QuadIn).OnEnd = () => Menu = false;
				}
			}
			
			if (!Paused) {
				if (!died && !Run.Won) {
					Run.Time += (float) Engine.GameTime.ElapsedGameTime.TotalSeconds;
				}

				Physics.Update(dt);
				base.Update(dt);
			} else {
				Ui.Update(dt);
			}

			var found = false;

			foreach (var t in Camera.Instance.Targets) {
				if (t.Entity is Player) {
					found = true;
					break;
				}
			}

			if (found) {
				Camera.Instance.Zoom += ((Input.IsDown(Controls.Map, GamepadComponent.Current) ? 0.5f : 1f) - Camera.Instance.Zoom) * dt * 10;
			}

			console.Update(dt);

			foreach (var p in Area.Tagged[Tags.LocalPlayer]) {
				var controller = GamepadComponent.Current;
				
				if (painting != null) {
					if (Input.WasPressed(Controls.Pause, controller) || Input.WasPressed(Controls.Interact, controller) ||
					    Input.WasPressed(Controls.Use, controller)) {
						painting.Remove();
					}
				} else {
					if (doneAnimatingPause) {
						var did = false;

						if (DialogComponent.Talking == null) {
							if (Input.WasPressed(Controls.Pause, controller)) {
								if (SkipPause) {
									SkipPause = false;
								} else if (Paused) {
									if (UiControl.Focused == null && currentBack == null) {
										Paused = false;
										did = true;
									}
								} else {
									Paused = true;
									did = true;
								}
							}

							if (!did && Paused && Input.WasPressed(Controls.UiBack, controller)) {
								if (Settings.UiSfx) {
									Audio.PlaySfx("ui_exit", 0.5f);
								}

								if (UiControl.Focused != null) {
									UiControl.Focused.Cancel();
								} else if (currentBack != null) {
									currentBack.Click(currentBack);
								} else {
									Paused = false;
								}
							}
						}
					}
				}

				if (controller == null || Paused) {
					continue;
				}
				
				var stick = controller.GetRightStick();

				var dx = stick.X;
				var dy = stick.Y;
				var d = (float) Math.Sqrt(dx * dx + dy * dy);
				
				if (d > 1) {
					stick /= d;
				} else {
					stick *= d;
				}

				var l = stick.Length();
				
				if (l > 0.25f) {
					var target = MathUtils.CreateVector(Math.Atan2(dy, dx), 1f);
					dx = target.X - stickOffset.X;
					dy = target.Y - stickOffset.Y;
					
					d = (float) Math.Sqrt(dx * dx + dy * dy);

					if (d > 1) {
						dx /= d;
						dy /= d;
					} else {
						dx *= d;
						dy *= d;
					}
					
					stickOffset += l * new Vector2(dx, dy) * dt * 10f * Settings.Sensivity;
					Input.Mouse.Position = Camera.Instance.CameraToScreen(p.Center + stickOffset * 48);
				}

				double a = 0;
				var pressed = false;

				if (controller.DPadLeftCheck) {
					a = Math.PI;
					pressed = true;
				} else if (controller.DPadDownCheck) {
					a = Math.PI / 2f;
					pressed = true;
				} else if (controller.DPadUpCheck) {
					a = Math.PI * 1.5f;
					pressed = true;
				} else if (controller.DPadRightCheck) {
					pressed = true;
				}

				if (pressed) {
					Input.Mouse.Position = Camera.Instance.CameraToScreen(p.Center + MathUtils.CreateVector(a, 48));
				}
			}

			if (Engine.Version.Test) {
				UpdateDebug(dt);
				Tilesets.Update();
			}

			Run.Update();
			
			if (Input.WasPressed(Controls.Fullscreen) || (Input.Keyboard.WasPressed(Keys.Enter) && (Input.Keyboard.IsDown(Keys.LeftAlt) || Input.Keyboard.IsDown(Keys.RightAlt)))) {
				if (Engine.Graphics.IsFullScreen) {
					Engine.Instance.SetWindowed(Display.Width * 3, Display.Height * 3);
				} else {
					Engine.Instance.SetFullscreen();
				}
				
				Settings.Fullscreen = Engine.Graphics.IsFullScreen;
			}

			TopUi.Update(dt);
		}

		private bool saving;
		
		private void TeleportTo(RoomType type) {
			var player = LocalPlayer.Locate(Area);
			var room = player.GetComponent<RoomComponent>().Room;

			foreach (var r in Area.Tagged[Tags.Room]) {
				if (r != room && ((Room) r).Type == type) {
					player.Center = r.Center;
					return;
				}
			}
		}

		public static bool ToolsEnabled = BK.Version.Dev;
		
		private void UpdateDebug(float dt) {
			if (Input.Keyboard.WasPressed(Keys.Home) || (Input.Keyboard.WasPressed(Keys.Tab) && Input.Keyboard.IsDown(Keys.LeftControl))) {
				ToolsEnabled = !ToolsEnabled;
				var player = LocalPlayer.Locate(Area);

				if (player != null) {
					TextParticle.Add(player, "Dev Tools", 1, true, !ToolsEnabled);
				}
			}
			
			if (!ToolsEnabled) {
				return;
			}
			
			if (Input.Blocked > 0) {
				return;
			}

			if (Input.Keyboard.WasPressed(Keys.NumPad9)) {				
				SaveManager.Delete(SaveType.Game, SaveType.Level, SaveType.Player);
				Run.StartNew();
				died = true;

				Run.NextDepth = Run.Depth;

				return;
			}

			if (Input.Keyboard.IsDown(Keys.LeftControl)) {
				if (Input.Keyboard.WasPressed(Keys.D0)) {
					Run.Depth = 0;
				}
				
				if (Input.Keyboard.WasPressed(Keys.D1)) {
					Run.Depth = 1;
				}
				
				if (Input.Keyboard.WasPressed(Keys.D2)) {
					Run.Depth = 3;
				}
				
				if (Input.Keyboard.WasPressed(Keys.D3)) {
					Run.Depth = 5;
				}
				
				if (Input.Keyboard.WasPressed(Keys.D4)) {
					Run.Depth = 7;
				}
				
				if (Input.Keyboard.WasPressed(Keys.D5)) {
					Run.Depth = 9;
				}
			}

			if (Input.WasPressed(Controls.Fps)) {
				Settings.ShowFps = !Settings.ShowFps;
			}
			
			if (Input.Keyboard.WasPressed(Keys.F3)) {
				Settings.HideUi = !Settings.HideUi;
			}

			if (Input.Keyboard.WasPressed(Keys.F4)) {
				Settings.HideCursor = !Settings.HideCursor;
			}

			if (Input.Keyboard.WasPressed(Keys.F5)) {
				TeleportTo(RoomType.Treasure);
			}
			
			if (Input.Keyboard.WasPressed(Keys.F6)) {
				TeleportTo(RoomType.Shop);
			}
			
			if (Input.Keyboard.WasPressed(Keys.F7)) {
				TeleportTo(RoomType.Special);
			}

			if (Input.Keyboard.WasPressed(Keys.F8)) {
				TeleportTo(RoomType.Secret);
			}

			if (Input.Keyboard.WasPressed(Keys.F9)) {
				TeleportTo(RoomType.Boss);
			}

			if (Input.Keyboard.WasPressed(Keys.NumPad7)) {
				LocalPlayer.Locate(Area).Center = Input.Mouse.GamePosition;
			}

			if (Input.Keyboard.WasPressed(Keys.NumPad3)) {
				var level = Run.Level;

				for (var i = 0; i < level.Explored.Length; i++) {
					level.Explored[i] = true;
				}
			}

			if (Input.Keyboard.WasPressed(Keys.NumPad1)) {
				GlobalSave.ResetControlKnowldge();
			}

			if (Input.Keyboard.WasPressed(Keys.NumPad0)) {
				Camera.Instance.Detached = !Camera.Instance.Detached;
			}

			if (Camera.Instance.Detached) {
				float speed = dt * 120f;
				
				if (Input.Keyboard.IsDown(Keys.NumPad4)) {
					Camera.Instance.PositionX -= speed;
				}
				
				if (Input.Keyboard.IsDown(Keys.NumPad6)) {
					Camera.Instance.PositionX += speed;
				}
				
				if (Input.Keyboard.IsDown(Keys.NumPad8)) {
					Camera.Instance.PositionY -= speed;
				}
				
				if (Input.Keyboard.IsDown(Keys.NumPad2)) {
					Camera.Instance.PositionY += speed;
				}
			}
		}

		private void PrerenderShadows() {
			var renderer = (PixelPerfectGameRenderer) Engine.Instance.StateRenderer;
			
			renderer.End();
			
			var c = Camera.Instance;
			var z = c.Zoom;
			var n = Math.Abs(z - 1) > 0.01f;
				
			if (n) {
				c.Zoom = 1;
				c.UpdateMatrices();
			}
			
			renderer.BeginShadows();

			foreach (var e in Area.Tagged[Tags.HasShadow]) {
				if (e.AlwaysVisible || e.OnScreen) {
					e.GetComponent<ShadowComponent>().Callback();
				}
			}
			
			renderer.EndShadows();

			if (n) {
				c.Zoom = z;
				c.UpdateMatrices();
			}
			
			renderer.Begin();
		}
		
		public static void RenderFog() {
			var shader = Shaders.Fog;
			Shaders.Begin(shader);

			var wind = WindFx.CalculateWind();
			
			shader.Parameters["time"].SetValue(Engine.Time * 0.01f);
			shader.Parameters["tx"].SetValue(wind.X * -0.1f);
			shader.Parameters["ty"].SetValue(wind.Y * -0.1f);
			shader.Parameters["cx"].SetValue(Camera.Instance.Position.X / 512f);
			shader.Parameters["cy"].SetValue(Camera.Instance.Position.Y / 512f);
		
			Graphics.Render(fog, Camera.Instance.TopLeft);
			
			Shaders.End();
		}
		
		public override void Render() {
			PrerenderShadows();
			base.Render();
			Physics.Render();
			editor.RenderInGame();
		}

		private float emeraldY = -20;
		
		public override void RenderUi() {
			if (!Settings.HideUi) {
				if (Run.Depth == 0 || emeraldY > -20) {
					var y = Run.Depth == 0 ? 0 : emeraldY;
					var str = $"{GlobalSave.Emeralds}";
					var xx = Display.UiWidth - emerald.Width - 8;

					Graphics.Render(emerald, new Vector2(xx, 8 + y));
					Graphics.Print(str, Font.Small, new Vector2(xx - 8 - Font.Small.MeasureString(str).Width, 9 + y));
				}
			}

			base.RenderUi();

			if (blackBarsSize > 0.01f) {
				Graphics.Render(black, Vector2.Zero, 0, Vector2.Zero, new Vector2(Display.UiWidth + 1, blackBarsSize));
				Graphics.Render(black, new Vector2(0, Display.UiHeight + 1 - blackBarsSize), 0, Vector2.Zero, new Vector2(Display.UiWidth + 1, blackBarsSize + 1));
			}

			if (!Settings.HideUi) {
				Graphics.Color = ColorUtils.HalfWhiteColor;
				Graphics.Print(v, Font.Small, new Vector2(Display.UiWidth + vx - 1, 0));
				Graphics.Color = ColorUtils.WhiteColor;
			}

			painting?.RenderUi();

			TopUi.Render();

			if (Settings.HideUi) {
				cursor.Render();
				return;
			}


			if (Menu && offset <= Display.UiHeight) {
				Graphics.Render(black, new Vector2(0, offset - Display.UiHeight), 0, Vector2.Zero, new Vector2(Display.UiWidth + 1, Display.UiHeight + 1));
				Graphics.Render(gardient, new Vector2(0, offset), 0, Vector2.Zero, new Vector2(Display.UiWidth + 1, (Display.UiHeight + 1) / 90f));
				
				Graphics.Print("Press X", Font.Small, Display.Height + 48 + (int) offset);
				
				LogoRenderer.Render(offset);
			}

			var x = 1;
			
			if (Settings.ShowFps) {
				var c = Engine.Instance.Counter.AverageFramesPerSecond;
				Color color;

				if (c >= 55) {
					color = new Color(0f, 1f, 0f, 1f);
				} else if (c >= 45) {
					color = new Color(1f, 1f, 0f, 1f);
				} else {
					color = new Color(1f, 0f, 0f, 1f);
				}
				
				Graphics.Color = color;
				var s = $"{c}";
				Graphics.Print(s, Font.Small, x, 1);
				x += (int) Font.Small.MeasureString(s).Width + 1;
				Graphics.Color = ColorUtils.WhiteColor;
			}

			if (Settings.SpeedrunTimer && Run.Statistics != null) {
				Graphics.Print(GetRunTime(), Font.Small, x, 1);
			}

			// Graphics.Batch.DrawString(Font.Test, "Test 你 Test", Vector2.One, Color.White);
		}

		private string GetRunTime() {
			var t = Run.Statistics.Time;
			return $"{(Math.Floor(t / 3600f) + "").PadLeft(2, '0')}:{(Math.Floor(t / 60f) + "").PadLeft(2, '0')}:{(Math.Floor(t % 60f) + "").PadLeft(2, '0')}";
		}

		private void SetupUi() {
			TopUi.Add(new UiChat());
			
			UiButton.LastId = 0;
			
			var cam = new Camera(new FollowingDriver());
			TopUi.Add(cam);
			// Ui.Add(new AchievementBanner());
			
			editor = new EditorWindow(new Editor {
				Area = Area,
				Level = Run.Level,
				Camera = cam
			});

			var id = Run.Level.Biome.Id;

			if (id != Biome.Castle && id != Biome.Hub) {
				Achievements.Unlock($"bk:{id}");
			}

			cursor = new Cursor();
			TopUi.Add(cursor);
			
			Ui.Add(indicator = new SaveIndicator());

			var player = LocalPlayer.Locate(Area);

			console = new Console(Area);

			if (player != null) {
				Ui.Add(new UiInventory(player));
			}

			TopUi.Add(pauseMenu = new UiPane {
				Y = -Display.UiHeight	
			});

			var space = 24f;
			var start = Display.UiHeight * 0.5f;

			pauseMenu.Add(new UiLabel {
				Label = Level.GetDepthString(),
				RelativeCenterX = Display.UiWidth / 2f,
				RelativeCenterY = TitleY,
				AngleMod = 0
			});

			if (Run.Depth > 0) {
				pauseMenu.Add(seedLabel = new UiButton {
					Font = Font.Small,
					Selectable = false,
					Label = $"Seed: {Run.Seed}",
					RelativeCenterX = Display.UiWidth / 2f,
					RelativeCenterY = BackY,
					AngleMod = 0,
					Click = b => {
						b.LocaleLabel = "copied_to_clipboard";

						try {
							// Needs xclip on linux
							TextCopy.Clipboard.SetText(Run.Seed);
						} catch (Exception e) {
							Log.Error(e);
						}

						Timer.Add(() => { b.Label = $"Seed: {Run.Seed}"; }, 0.5f);
					}
				});
			}

			pauseBack = currentBack = (UiButton) pauseMenu.Add(new UiButton {
				LocaleLabel = "resume",
				RelativeCenterX = Display.UiWidth / 2f,
				RelativeCenterY = start - space,
				Click = b => Paused = false
			});
			
			pauseMenu.Add(new UiButton {
				LocaleLabel = "settings",
				RelativeCenterX = Display.UiWidth / 2f,
				RelativeCenterY = start,
				Click = b => {
					currentBack = settingsBack;
					Tween.To(-Display.UiWidth, pauseMenu.X, x => pauseMenu.X = x, PaneTransitionTime).OnEnd = SelectFirst;
				}
			});

			if (Run.Depth > 0) {
				pauseMenu.Add(new UiButton {
						LocaleLabel = "new_run",
						RelativeCenterX = Display.UiWidth / 2f,
						RelativeCenterY = start + space,
						Type = ButtonType.Exit,
						Click = b => GoConfirm("start_new_run", () => {
							Run.StartNew();
						}, () => {
							currentBack = pauseBack;
							pauseMenu.Enabled = true;

							Tween.To(0, pauseMenu.X, x => pauseMenu.X = x, PaneTransitionTime).OnEnd = () => {
								pauseMenu.Remove(confirmationPane);
								confirmationPane = null;	
								SelectFirst();
							};
						})
				});
			} else if (Run.Depth == 0) {
				pauseMenu.Add(new UiButton {
					LocaleLabel = "exit",
					Type = ButtonType.Exit,
					RelativeCenterX = Display.UiWidth / 2f,
					RelativeCenterY = BackY,
					Click = b => {
						Engine.Instance.Quit();
					}
				});
			}

			if (Run.Depth != 0) {
				pauseMenu.Add(new UiButton {
						LocaleLabel = "back_to_castle",
						RelativeCenterX = Display.UiWidth / 2f,
						RelativeCenterY = start + space * 2,
						Type = ButtonType.Exit,
						Click = b => Run.Depth = 0
				});
			}

			AddSettings();
			
			pauseMenu.Setup();
			
			TopUi.Add(gameOverMenu = new UiPane {
				Y = -Display.UiHeight
			});
			
			space = 20f;
			start = (Display.UiHeight) / 2f - space;

			killedLabel = (UiLabel) gameOverMenu.Add(new UiLabel {
				Font = Font.Small,
				LocaleLabel = "killed_by",
				RelativeCenterX = Display.UiWidth * 0.75f,
				RelativeCenterY = start - space,
				Tints = false,
				Clickable = false
			});

			Killer = (UiAnimation) gameOverMenu.Add(new UiAnimation {
				RelativeCenterX = Display.UiWidth * 0.75f,
				RelativeY = start,
				Clickable = false
			});
			
			gameOverMenu.Add(new UiButton {
				LocaleLabel = "restart",
				RelativeCenterX = Display.UiWidth / 2f,
				// RelativeCenterY = start + space * 3,
				RelativeCenterY = BackY,

				Click = b => {
					gameOverMenu.Enabled = false;
					Run.StartNew(Run.Depth == -2 ? -2 : /*-1*/ 0);
				}
			});

			gameOverMenu.Setup();
			gameOverMenu.Enabled = false;

			if (Run.Depth > 0 && Run.Level != null && !Menu) {
				Ui.Add(new UiBanner(Level.GetDepthString()));
			}
		}

		private void AddSettings() {
			var sx = Display.UiWidth * 1.5f;
			var space = 24f;
			var sy = Display.UiHeight * 0.5f - space * 0.5f;
			
			pauseMenu.Add(new UiLabel {
				LocaleLabel = "settings",
				RelativeCenterX = sx,
				RelativeCenterY = TitleY
			});

			pauseMenu.Add(new UiButton {
				LocaleLabel = "game",
				RelativeCenterX = sx,
				RelativeCenterY = sy - space,
				Click = b => {
					currentBack = gameBack;
					gameSettings.Enabled = true;
					Tween.To(-Display.UiWidth * 2, pauseMenu.X, x => pauseMenu.X = x, PaneTransitionTime).OnEnd = SelectFirst;
				}
			});
			
			pauseMenu.Add(new UiButton {
				LocaleLabel = "graphics",
				RelativeCenterX = sx,
				RelativeCenterY = sy,
				Click = b => {
					currentBack = graphicsBack;
					graphicsSettings.Enabled = true;
					Tween.To(-Display.UiWidth * 2, pauseMenu.X, x => pauseMenu.X = x, PaneTransitionTime).OnEnd = SelectFirst;
				}
			});
			
			pauseMenu.Add(new UiButton {
				LocaleLabel = "audio",
				RelativeCenterX = sx,
				RelativeCenterY = sy + space,
				Click = b => {
					currentBack = audioBack;
					audioSettings.Enabled = true;
					Tween.To(-Display.UiWidth * 2, pauseMenu.X, x => pauseMenu.X = x, PaneTransitionTime).OnEnd = SelectFirst;
				}
			});
			
			pauseMenu.Add(new UiButton {
				LocaleLabel = "input",
				RelativeCenterX = sx,
				RelativeCenterY = sy + space * 2,
				Click = b => {
					currentBack = inputBack;
					inputSettings.Enabled = true;
					Tween.To(-Display.UiWidth * 2, pauseMenu.X, x => pauseMenu.X = x, PaneTransitionTime).OnEnd = SelectFirst;
				}
			});
			
			settingsBack = (UiButton) pauseMenu.Add(new UiButton {
				LocaleLabel = "back",
				Type = ButtonType.Exit,
				RelativeCenterX = sx,
				RelativeCenterY = BackY,
				Click = b => {
					new Thread(() => {
						try {
							SaveManager.Save(Area, SaveType.Global);
						} catch (Exception e) {
							Log.Error(e);
						}
					}) {
						Priority = ThreadPriority.Lowest
					}.Start();
					
					currentBack = pauseBack;
					pauseMenu.Enabled = true;
					
					Tween.To(0, pauseMenu.X, x => pauseMenu.X = x, PaneTransitionTime).OnEnd = () => {
						SelectFirst();
					};
				}
			});
			
			pauseMenu.Enabled = false;
			
			AddGameSettings();
			AddGraphicsSettings();
			AddAudioSettings();
			AddInputSettings();
		}

		private UiButton pauseBack;
		private UiButton settingsBack;
		private UiButton audioBack;
		private UiButton graphicsBack;
		private UiButton gameBack;

		private void AddGameSettings() {
			pauseMenu.Add(gameSettings = new UiPane {
				RelativeX = Display.UiWidth * 2	
			});
			
			var sx = Display.UiWidth * 0.5f;
			var space = 20f;
			var sy = Display.UiHeight * 0.5f - space * 1.5f;
			
			gameSettings.Add(new UiLabel {
				LocaleLabel = "game",
				RelativeCenterX = sx,
				RelativeCenterY = TitleY
			});

			gameSettings.Add(new UiCheckbox {
				Name = "autosave",
				On = Settings.Autosave,
				RelativeX = sx,
				RelativeCenterY = sy - space * 2,
				Click = b => {
					Settings.Autosave = ((UiCheckbox) b).On;
				}
			});

			gameSettings.Add(new UiCheckbox {
				Name = "autopause",
				On = Settings.Autopause,
				RelativeX = sx,
				RelativeCenterY = sy - space,
				Click = b => {
					Settings.Autopause = ((UiCheckbox) b).On;
				}
			});

			gameSettings.Add(new UiCheckbox {
				Name = "speedrun_timer",
				On = Settings.SpeedrunTimer,
				RelativeX = sx,
				RelativeCenterY = sy,
				Click = b => {
					Settings.SpeedrunTimer = ((UiCheckbox) b).On;
				}
			});

			var presses = 0;

			gameSettings.Add(new UiCheckbox {
				Name = "vegan_mode",
				On = Settings.Vegan,
				RelativeX = sx,
				RelativeCenterY = sy + space,
				Click = b => {
					presses++;
					Settings.Vegan = ((UiCheckbox) b).On;

					Log.Info($"Click #{presses}");
					
					if (presses == 20) {
						Log.Debug("Unlock npcs!");
						
						GlobalSave.Put(ShopNpc.AccessoryTrader, true);
						GlobalSave.Put(ShopNpc.ActiveTrader, true);
						GlobalSave.Put(ShopNpc.HatTrader, true);
						GlobalSave.Put(ShopNpc.WeaponTrader, true);
						
						GlobalSave.Put("control_use", true);
						GlobalSave.Put("control_swap", true);
						GlobalSave.Put("control_roll", true);
						GlobalSave.Put("control_interact", true);
						GlobalSave.Put("control_duck", true);
					}
				}
			});

			gameSettings.Add(new UiCheckbox {
				Name = "blood_n_gore",
				On = Settings.Blood,
				RelativeX = sx,
				RelativeCenterY = sy + space * 2,
				Click = b => {
					Settings.Blood = ((UiCheckbox) b).On;
				}
			});
			
			gameSettings.Add(new UiButton {
				LocaleLabel = "reset_settings",
				RelativeCenterX = sx,
				RelativeCenterY = sy + space * 3.5f,
				Click = b => {
					GoConfirm("reset_settings_dis", () => {
						currentBack = settingsBack;
						gameSettings.Enabled = true;
						
						new Thread(() => {
							try {
								Settings.Generate();
								gameBack.Click(gameBack);
							} catch (Exception e) {
								Log.Error(e);
							}
						}) {
							Priority = ThreadPriority.Lowest
						}.Start();
					}, () => {
						currentBack = gameBack;
						gameSettings.Enabled = true;

						Tween.To(Display.UiWidth * -2, pauseMenu.X, x => pauseMenu.X = x, PaneTransitionTime).OnEnd = () => {
							pauseMenu.Remove(confirmationPane);
							confirmationPane = null;	
							SelectFirst();
						};
					});
				}
			});
			
			gameSettings.Add(new UiButton {
				LocaleLabel = "reset_progress",
				RelativeCenterX = sx,
				RelativeCenterY = sy + space * 4.5f,
				Click = b => {
					GoConfirm("reset_progress_dis", () => {
						currentBack = settingsBack;
						gameSettings.Enabled = true;
						
						new Thread(() => {
							try {
								SaveManager.Delete(SaveType.Player, SaveType.Level, SaveType.Game, SaveType.Global);
								SaveManager.DeleteCloudSaves();

								try {
 									SteamUserStats.ResetAll(true);
								} catch (Exception e) {
									
								}
								
								Achievements.LoadState();
								GlobalSave.Emeralds = 0;
								
								Run.StartingNew = true;
								Run.NextDepth = 0;
								Run.IntoMenu = true;
							} catch (Exception e) {
								Log.Error(e);
							}
						}) {
							Priority = ThreadPriority.Lowest
						}.Start();
					}, () => {
						currentBack = gameBack;
						gameSettings.Enabled = true;

						Tween.To(Display.UiWidth * -2, pauseMenu.X, x => pauseMenu.X = x, PaneTransitionTime).OnEnd = () => {
							confirmationPane.Active = false;
							pauseMenu.Remove(confirmationPane);
							confirmationPane = null;	
							SelectFirst();
						};
					});
				}
			});

			if (Run.Depth > -2) {
				gameSettings.Add(new UiButton {
						LocaleLabel = "tutorial",
						RelativeCenterX = sx,
						RelativeCenterY = sy + space * 5.5f,
						Click = b => { Run.Depth = -2; }
				});
			}

			gameBack = (UiButton) gameSettings.Add(new UiButton {
				LocaleLabel = "back",
				Type = ButtonType.Exit,
				RelativeCenterX = sx,
				RelativeCenterY = BackY,
				Click = b => {
					currentBack = settingsBack;
					Tween.To(-Display.UiWidth, pauseMenu.X, x => pauseMenu.X = x, PaneTransitionTime).OnEnd = () => {
						SelectFirst();
						gameSettings.Enabled = false;
					};
				}
			});
			
			gameSettings.Enabled = false;
		}

		private void GoConfirm(string text, Action callback, Action nope) {
			pauseMenu.Add(confirmationPane = new UiPane {
				RelativeX = Display.UiWidth * 3
			});
			
			var sx = Display.UiWidth * 0.5f;
			var sy = Display.UiHeight * 0.5f;
			var space = 32;
			
			confirmationPane.Add(new UiLabel {
				Font = Font.Small,
				AngleMod = 0,
				LocaleLabel = "are_you_sure",
				RelativeCenterX = sx,
				RelativeCenterY = sy - space * 1.5f
			});

			confirmationPane.Add(new UiLabel {
				Font = Font.Small,
				AngleMod = 0,
				LocaleLabel = text,
				RelativeCenterX = sx,
				RelativeCenterY = sy - space
			});

			var spx = 32;
			
			confirmationPane.Add(new UiButton {
				LocaleLabel = "yes",
				RelativeCenterX = sx + spx,
				RelativeCenterY = sy + space,
				Click = b => {
					callback();
				}
			});
			
			currentBack = (UiButton) confirmationPane.Add(new UiButton {
				LocaleLabel = "no",
				RelativeCenterX = sx - spx,
				RelativeCenterY = sy + space,
				Click = b => {
					nope();
				}
			});
			
			Tween.To(Display.UiWidth * -3, pauseMenu.X, x => pauseMenu.X = x, PaneTransitionTime).OnEnd = () => {
				SelectFirst();
				gameSettings.Enabled = false;
			};
		}
		
		private void AddGraphicsSettings() {
			pauseMenu.Add(graphicsSettings = new UiPane {
				RelativeX = Display.UiWidth * 2	
			});
			
			var sx = Display.UiWidth * 0.5f;
			var space = 18f;
			var sy = Display.UiHeight * 0.5f - space * 2;
			
			graphicsSettings.Add(new UiLabel {
				LocaleLabel = "graphics",
				RelativeCenterX = sx,
				RelativeCenterY = TitleY
			});

			graphicsSettings.Add(new UiCheckbox {
				Name = "fullscreen",
				On = Engine.Graphics.IsFullScreen,
				RelativeX = sx,
				RelativeCenterY = sy - space * 2,
				Click = b => {
					Settings.Fullscreen = ((UiCheckbox) b).On;

					if (Settings.Fullscreen) {
						Engine.Instance.SetFullscreen();
					} else {
						Engine.Instance.SetWindowed(Display.Width * 3, Display.Height * 3);
					}
				},
				
				OnUpdate = c => {
					((UiCheckbox) c).On = Engine.Graphics.IsFullScreen;
					Settings.Fullscreen = ((UiCheckbox) c).On;
				}
			});

			graphicsSettings.Add(new UiCheckbox {
				Name = "vsync",
				On = Settings.Vsync,
				RelativeX = sx,
				RelativeCenterY = sy - space,
				Click = b => {
					Settings.Vsync = ((UiCheckbox) b).On;
					Engine.Graphics.SynchronizeWithVerticalRetrace = Settings.Vsync;
					Engine.Graphics.ApplyChanges();
				}
			});

			graphicsSettings.Add(new UiCheckbox {
				Name = "fps",
				On = Settings.ShowFps,
				RelativeX = sx,
				RelativeCenterY = sy,
				Click = b => {
					Settings.ShowFps = ((UiCheckbox) b).On;
				},
				
				OnUpdate = c => {
					((UiCheckbox) c).On = Settings.ShowFps;
				}
			});
			
			graphicsSettings.Add(new UiChoice {
				Name = "cursor",
				Options = new [] {
					"A", "B", "C", "D", "E", "F", "G", "J", "K"
				},
				
				Option = Settings.Cursor,
				RelativeX = sx,
				RelativeCenterY = sy + space,
				
				Click = c => {
					Settings.Cursor = ((UiChoice) c).Option;
				}
			});

			UiSlider.Make(graphicsSettings, sx, sy + space * 2, "screenshake", (int) (Settings.Screenshake * 100), 1000).OnValueChange = s => {
				Settings.Screenshake = s.Value / 100f;
				ShakeComponent.Modifier = Settings.Screenshake;

				if (s.Value == 1000) {
					Achievements.Unlock("bk:overshake");
				}
			};
				
			UiSlider.Make(graphicsSettings, sx, sy + space * 3, "flash_frames", (int) (Settings.FlashFrames * 100)).OnValueChange = s => {
				Settings.FlashFrames = s.Value / 100f;
				Engine.FlashModifier = Settings.FlashFrames;
			};
			
			UiSlider.Make(graphicsSettings, sx, sy + space * 4, "freeze_frames", (int) (Settings.FreezeFrames * 100)).OnValueChange = s => {
				Settings.FreezeFrames = s.Value / 100f;
				Engine.FreezeModifier = Settings.FreezeFrames;
			};
			
			UiSlider.Make(graphicsSettings, sx, sy + space * 5, "scale", (int) (Settings.GameScale * 100), 200, 100).OnValueChange = s => {
				Tween.To(s.Value / 100f, Settings.GameScale, x => Settings.GameScale = x, 0.3f);
			};
			
			UiSlider.Make(graphicsSettings, sx, sy + space * 6, "floor_brightness", (int) (Settings.FloorDarkness * 100), 100).OnValueChange = s => {
				Tween.To(s.Value / 100f, Settings.FloorDarkness, x => Settings.FloorDarkness = x, 0.3f);
			};
			
			graphicsBack = (UiButton) graphicsSettings.Add(new UiButton {
				LocaleLabel = "back",
				Type = ButtonType.Exit,
				RelativeCenterX = sx,
				RelativeCenterY = BackY,
				Click = b => {
					currentBack = settingsBack;
					Tween.To(-Display.UiWidth, pauseMenu.X, x => pauseMenu.X = x, PaneTransitionTime).OnEnd = () => {
						SelectFirst();
						graphicsSettings.Enabled = false;
					};
				}
			});
			
			graphicsSettings.Enabled = false;
		}

		private void AddAudioSettings() {
			pauseMenu.Add(audioSettings = new UiPane {
				RelativeX = Display.UiWidth * 2	
			});
			
			var sx = Display.UiWidth * 0.5f;
			var space = 20f;
			var sy = Display.UiHeight * 0.5f - space;
			
			audioSettings.Add(new UiLabel {
				LocaleLabel = "audio",
				RelativeCenterX = sx,
				RelativeCenterY = TitleY
			});
			
			UiSlider.Make(audioSettings, sx, sy - space, "master_volume", (int) (Settings.MasterVolume * 100)).OnValueChange = s => {
				Settings.MasterVolume = s.Value / 100f;
			};
			
			UiSlider.Make(audioSettings, sx, sy, "music", (int) (Settings.MusicVolume * 100)).OnValueChange = s => {
				Settings.MusicVolume = s.Value / 100f;
			};
			
			UiSlider.Make(audioSettings, sx, sy + space, "sfx", (int) (Settings.SfxVolume * 100)).OnValueChange = s => {
				Settings.SfxVolume = s.Value / 100f;
			};

			audioSettings.Add(new UiCheckbox {
				Name = "ui_sfx",
				On = Settings.UiSfx,
				RelativeX = sx,
				RelativeCenterY = sy + space * 2.5f,
				Click = b => {
					Settings.UiSfx = ((UiCheckbox) b).On;
				}
			});
			
			audioBack = (UiButton) audioSettings.Add(new UiButton {
				LocaleLabel = "back",
				Type = ButtonType.Exit,
				RelativeCenterX = sx,
				RelativeCenterY = BackY,
				Click = b => {
					currentBack = settingsBack;
					Tween.To(-Display.UiWidth, pauseMenu.X, x => pauseMenu.X = x, PaneTransitionTime).OnEnd = () => {
						SelectFirst();
						audioSettings.Enabled = false;
					};
				}
			});
			
			audioSettings.Enabled = false;
		}

		public void AddInputSettings() {
			pauseMenu.Add(inputSettings = new UiPane {
				RelativeX = Display.UiWidth * 2	
			});
			
			var sx = Display.UiWidth * 0.5f;
			var space = 20f;
			var sy = Display.UiHeight * 0.5f - space * 0.5f;
			
			inputSettings.Add(new UiLabel {
				LocaleLabel = "input",
				RelativeCenterX = sx,
				RelativeCenterY = TitleY
			});

			var first = true;
			UiButton gamepad = null;
			
			inputSettings.Add(new UiChoice {
				Name = "gamepad",
				
				RelativeX = sx,
				RelativeCenterY = sy - space,
				
				Options = new [] {"none"},
				
				Click = c => {
					// var i = ((UiChoice) c).Option;
					var p = LocalPlayer.Locate(Area);
					// var e = i == GamepadData.Identifiers.Length;
					
					// Settings.Gamepad = e ? null : GamepadData.Identifiers[i];
					if (p != null) {
						var d = p.GetComponent<GamepadComponent>();
							
						d.Controller = null;
						d.GamepadId = null;
					}
				},
				
				OnUpdate = uc => {
					
					if (!first && !GamepadData.WasChanged) {
						return;
					}

					var con = new List<string>();
					var id = new List<string>();
					var cur = 0;
			
					for (var i = 0; i < 4; i++) {
						if (Input.Gamepads[i].Attached) {
							var d = GamePad.GetCapabilities(i);
					
							if (d.GamePadType == GamePadType.GamePad) {
								id.Add(d.Identifier);
								con.Add(d.DisplayName);
	
								if (Settings.Gamepad == d.Identifier) {
									cur = i;
								}
							}
						}
					}

					GamepadData.Identifiers = id.ToArray();
					con.Add("none");

					uc.Options = con.ToArray();
					uc.Option = cur;

					if (first && cur == con.Count - 1) {
						// gamepad.Visible = gamepad.Active = false;
					}
					
					first = false;
					GamepadData.Identifiers = id.ToArray();
				}
			});
			
			sy += space * 0.5f;
			
			inputSettings.Add(new UiButton {
				LocaleLabel = "keyboard_controls",
				RelativeCenterX = sx,
				RelativeCenterY = sy,
				Click = b => {
					currentBack = keyboardBack;
					keyboardSettings.Enabled = true;
					Tween.To(-Display.UiWidth * 3, pauseMenu.X, x => pauseMenu.X = x, PaneTransitionTime).OnEnd = () => {
						SelectFirst();
						inputSettings.Enabled = false;
					};
				}
			});
			
			gamepad = (UiButton) inputSettings.Add(new UiButton {
				LocaleLabel = "gamepad_controls",
				RelativeCenterX = sx,
				RelativeCenterY = sy + space,
				Click = b => {
					currentBack = gamepadBack;
					gamepadSettings.Enabled = true;
					Tween.To(-Display.UiWidth * 3, pauseMenu.X, x => pauseMenu.X = x, PaneTransitionTime).OnEnd = () => {
						SelectFirst();
						inputSettings.Enabled = false;
					};
				}
			});
			
			// gamepad.Visible = GamepadComponent.Current != null || Settings.Gamepad != null;
			
			inputBack = (UiButton) inputSettings.Add(new UiButton {
				LocaleLabel = "back",
				Type = ButtonType.Exit,
				RelativeCenterX = sx,
				RelativeCenterY = BackY,
				Click = b => {
					currentBack = settingsBack;
					Tween.To(-Display.UiWidth, pauseMenu.X, x => pauseMenu.X = x, PaneTransitionTime).OnEnd = () => {
						SelectFirst();
						inputSettings.Enabled = false;
					};
				}
			});

			inputSettings.Enabled = false;

			AddKeyboardSettings();
			AddGamepadSettings();
		}

		private void AddKeyboardSettings() {
			pauseMenu.Add(keyboardSettings = new UiPane {
				RelativeX = Display.UiWidth * 3
			});
			
			var sx = Display.UiWidth * 0.5f;
			var space = 20f;
			var spX = 96f;
			var sy = Display.UiHeight * 0.5f + space * 1.5f;
			
			keyboardSettings.Add(new UiLabel {
				LocaleLabel = "keyboard",
				RelativeCenterX = sx,
				RelativeCenterY = TitleY
			});
			
			keyboardSettings.Add(new UiControl {
					Key = Controls.Left,
					RelativeX = sx - spX,
					RelativeCenterY = sy - space * 4,
			});
			
			keyboardSettings.Add(new UiControl {
					Key = Controls.Right,
					RelativeX = sx + spX,
					RelativeCenterY = sy - space * 4,
			});

			keyboardSettings.Add(new UiControl {
					Key = Controls.Up,
					RelativeX = sx - spX,
					RelativeCenterY = sy - space * 3,
			});
			
			keyboardSettings.Add(new UiControl {
					Key = Controls.Down,
					RelativeX = sx + spX,
					RelativeCenterY = sy - space * 3,
			});

			keyboardSettings.Add(new UiControl {
				Key = Controls.Use,
				RelativeX = sx - spX,
				RelativeCenterY = sy - space * 2,
			});
			
			keyboardSettings.Add(new UiControl {
				Key = Controls.Active,
				RelativeX = sx + spX,
				RelativeCenterY = sy - space * 2,
			});

			keyboardSettings.Add(new UiControl {
				Key = Controls.Bomb,
				RelativeX = sx - spX,
				RelativeCenterY = sy - space,
			});
			
			keyboardSettings.Add(new UiControl {
				Key = Controls.Interact,
				RelativeX = sx + spX,
				RelativeCenterY = sy - space,
			});
			
			keyboardSettings.Add(new UiControl {
				Key = Controls.Swap,
				RelativeX = sx - spX,
				RelativeCenterY = sy,
			});
			
			keyboardSettings.Add(new UiControl {
				Key = Controls.Roll,
				RelativeX = sx + spX,
				RelativeCenterY = sy,
			});
			
			keyboardSettings.Add(new UiControl {
				Key = Controls.Duck,
				RelativeX = sx,
				RelativeCenterY = sy + space,
			});
			
			keyboardBack = (UiButton) keyboardSettings.Add(new UiButton {
				LocaleLabel = "back",
				Type = ButtonType.Exit,
				RelativeCenterX = sx,
				RelativeCenterY = BackY,
				Click = b => {
					inputSettings.Enabled = true;
					currentBack = inputBack;
					Tween.To(Display.UiWidth * -2, pauseMenu.X, x => pauseMenu.X = x, PaneTransitionTime).OnEnd = () => {
						SelectFirst();
						keyboardSettings.Enabled = false;
					};
				}
			});

			keyboardSettings.Enabled = false;
		}

		private void AddGamepadSettings() {
			pauseMenu.Add(gamepadSettings = new UiPane {
				RelativeX = Display.UiWidth * 3
			});
			
			var sx = Display.UiWidth * 0.5f;
			var space = 20f;
			var spX = 96f;
			var sy = Display.UiHeight * 0.5f + space * 0.5f;
			
			gamepadSettings.Add(new UiLabel {
				LocaleLabel = "gamepad",
				RelativeCenterX = sx,
				RelativeCenterY = TitleY
			});

			var g = LocalPlayer.Locate(Area)?.GetComponent<GamepadComponent>();

			gamepadSettings.Add(new UiControl {
				Key = Controls.Use,
				Gamepad = true,
				GamepadComponent = g,
				RelativeX = sx - spX,
				RelativeCenterY = sy - space * 3,
			});
			
			gamepadSettings.Add(new UiControl {
				Key = Controls.Active,
				Gamepad = true,
				GamepadComponent = g,
				RelativeX = sx + spX,
				RelativeCenterY = sy - space * 3,
			});

			gamepadSettings.Add(new UiControl {
				Key = Controls.Bomb,
				Gamepad = true,
				GamepadComponent = g,
				RelativeX = sx - spX,
				RelativeCenterY = sy - space * 2,
			});
			
			gamepadSettings.Add(new UiControl {
				Key = Controls.Interact,
				Gamepad = true,
				GamepadComponent = g,
				RelativeX = sx + spX,
				RelativeCenterY = sy - space * 2,
			});
			
			gamepadSettings.Add(new UiControl {
				Key = Controls.Swap,
				Gamepad = true,
				GamepadComponent = g,
				RelativeX = sx - spX,
				RelativeCenterY = sy - space,
			});
			
			gamepadSettings.Add(new UiControl {
				Key = Controls.Roll,
				Gamepad = true,
				GamepadComponent = g,
				RelativeX = sx + spX,
				RelativeCenterY = sy - space,
			});
			
			gamepadSettings.Add(new UiControl {
				Key = Controls.Duck,
				Gamepad = true,
				GamepadComponent = g,
				RelativeX = sx,
				RelativeCenterY = sy,
			});
			
			gamepadSettings.Add(new UiCheckbox {
				Name = "vibration",
				On = Settings.Vibrate,
				RelativeX = sx,
				RelativeCenterY = sy + space * 2,
				Click = b => {
					Settings.Vibrate = ((UiCheckbox) b).On;
				},
				
				OnUpdate = c => {
					((UiCheckbox) c).On = Settings.Vibrate;
				}
			});
			
			UiSlider.Make(gamepadSettings, sx, sy + space * 3, "sensivity", (int) (Settings.Sensivity * 100), 200, 10).OnValueChange = s => {
				Settings.Sensivity = s.Value / 100f;
			};

			gamepadBack = (UiButton) gamepadSettings.Add(new UiButton {
				LocaleLabel = "back",
				Type = ButtonType.Exit,
				RelativeCenterX = sx,
				RelativeCenterY = BackY,
				Click = b => {
					currentBack = inputBack;
					inputSettings.Enabled = true;
					Tween.To(Display.UiWidth * -2, pauseMenu.X, x => pauseMenu.X = x, PaneTransitionTime).OnEnd = () => {
						SelectFirst();
						gamepadSettings.Enabled = false;
					};
				}
			});
			
			gamepadSettings.Enabled = false;
		}

		public void AnimateDoneScreen() {
			if (Run.Type == RunType.Daily) {
				Player.StartingItem = null;
				Player.StartingWeapon = null;
			}

			if (Run.Won) {
				if (Run.Type == RunType.BossRush) {
					Achievements.Unlock("bk:boss_rush");
				} else if (Run.Type == RunType.Challenge) {
					GlobalSave.Put($"challenge_{Run.ChallengeId}", true);
					var count = 0;

					for (var i = 1; i <= 30; i++) {
						if (GlobalSave.IsTrue($"challenge_{i}")) {
							count++;
						}
					}
					
					Achievements.SetProgress("bk:10_challenges", Math.Min(10, count), 10);
					Achievements.SetProgress("bk:20_challenges", Math.Min(20, count), 20);
					Achievements.SetProgress("bk:30_challenges", Math.Min(30, count), 30);
				} else if (Run.Type == RunType.Daily) {
					Achievements.Unlock("bk:daily");
				}
			}

			gameOverMenu.Enabled = true;	
			GlobalSave.Put("played_once", true);

			gameOverMenu.Add(new UiLabel {
				LocaleLabel = Run.Won ? (BK.Demo ? "you_won_demo" : "won_message") : "death_message",
				RelativeCenterX = Display.UiWidth / 2f,
				RelativeCenterY = TitleY,
				Clickable = false
			});
			
			if (Run.Won) {
				killedLabel.Done = true;
				Killer.Done = true;
				
				new Thread(() => {
					SaveManager.Save(Area, SaveType.Statistics);
					SaveManager.Delete(SaveType.Player, SaveType.Level, SaveType.Game);
					SaveManager.Backup();
				}).Start();
			}

			Camera.Instance.Targets.Clear();

			var stats = new UiTable {
				Width = 128
			};

			gameOverMenu.Add(stats);

			stats.Add(Locale.Get("run_type"), Locale.Get($"run_{Run.Type.ToString().ToLower()}"));
			stats.Add(Locale.Get("seed"), Run.Seed);
			stats.Add(Locale.Get("time"), GetRunTime());
			stats.Add(Locale.Get("depth"), Run.Depth.ToString());
			stats.Add(Locale.Get("coins_collected"), Run.Statistics.CoinsObtained.ToString());
			stats.Add(Locale.Get("items_collected"), Run.Statistics.Items.Count.ToString());
			stats.Add(Locale.Get("damage_taken"), Run.Statistics.DamageTaken.ToString());
			stats.Add(Locale.Get("kills"), Run.Statistics.MobsKilled.ToString());
			stats.Add(Locale.Get("scourge"), Run.Scourge.ToString());
			stats.Add(Locale.Get("rooms_explored"), $"{Run.Statistics.RoomsExplored} / {Run.Statistics.RoomsTotal}");
			stats.Add(Locale.Get("distance_traveled"), $"{(Run.Statistics.TilesWalked / 1024f):0.0} {Locale.Get("km")}");

			Run.CalculateScore();
			Log.Info($"Run score is {Run.Score}");

			var newHigh = false;

			if (Run.Type == RunType.Challenge) {
				newHigh = GlobalSave.GetInt("high_score") < Run.Score;
				
				if (newHigh) {
					Log.Info("New highscore!");
					GlobalSave.Put("high_score", Run.Score);
				}
			}
			
			stats.Add(Locale.Get("score"), newHigh ? $"{Locale.Get("new_high_score")} {Run.Score}" : Run.Score.ToString());
			stats.Prepare();
			
			stats.RelativeCenterX = Display.UiWidth * 0.5f;
			stats.RelativeCenterY = Display.UiHeight * 0.5f;
			
			Audio.PlayMusic("Nostalgia", true);
			
			Tween.To(this, new {blur = 1}, 0.5f);
			Tween.To(0, gameOverMenu.Y, x => gameOverMenu.Y = x, 1f, Ease.BackOut).OnEnd = () => {
				SelectFirst();
			};
			
			OpenBlackBars();

			var board = "high_score";

			switch (Run.Type) {
				case RunType.Regular: {
					break;
				}
				
				case RunType.Daily: {
					board = $"daily_{Run.DailyId}";
					break;
				}

				case RunType.BossRush: {
					board = "boss_rush";
					break;
				}

				case RunType.Challenge: {
					board = $"challenge_{Run.ChallengeId}";
					break;
				}
			}

			Run.SubmitScore?.Invoke(Run.Score, board);
		}
		
		public void HandleDeath() {
			died = true;
				
			new Thread(() => {
				SaveManager.Save(Area, SaveType.Statistics);
				SaveManager.Delete(SaveType.Player, SaveType.Level, SaveType.Game);
				SaveManager.Backup();
			}).Start();
		}

		private TweenTask last;

		public bool HandleEvent(Event e) {
			if (e is GiveEmeraldsUse.GaveEvent ge) {
				Tween.To(0, emeraldY, x => emeraldY = x, 0.4f, Ease.BackOut).OnEnd = () => {
					Tween.Remove(last);
					
					last = Tween.To(-20, emeraldY, x => emeraldY = x, 0.3f, Ease.QuadIn);
					last.OnEnd = () => { last = null; };
					last.Delay = 3;
				};
				
				return false;
			}
			
			if (died || Run.Won) {
				return false;
			}
			
			if (e is DiedEvent de && de.Who is Mob) {
				Run.KillCount++;
			}

			return false;
		}

		public override void RenderNative() {
			if (!Console.Open) {
				return;
			}
			
			ImGuiHelper.Begin();
			
			console.Render();
			editor.Render();
			
			WindowManager.Render(Area);
			ImGuiHelper.End();
			
			Graphics.Batch.Begin();
			Graphics.Batch.DrawCircle(new CircleF(Mouse.GetState().Position, 3f), 8, Color.White);
			Graphics.Batch.End();
		}
	}
}
