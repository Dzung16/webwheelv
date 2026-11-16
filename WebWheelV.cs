#pragma warning disable 0162, 0429

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using GTA.UI;

using Control = GTA.Control;
using Screen = GTA.UI.Screen;

public class VehicleRadialMenu : Script
{
	// ----- Settings -----
	private Keys OpenKey = Keys.X;   // default menu key; change it in the menu (top-right button), saved as MenuKey in the ini
	private const float Sensitivity = 450f;     // mouse sensitivity inside the menu
	private static readonly float SlowMoScale = 0.2f;     // game speed while the menu is open and the slow-mo button is on (1.0 = normal, 0.2 = slow-mo)
	private static readonly bool DebugMessages = false;   // set to true to see on-screen debug messages
	private static readonly bool ShowKeybindFeedback = false;  // short on-screen messages for keybinds (when used / assigned)
	private static readonly bool ControllerSupport = true;   // gamepad (Xbox / XInput) buttons for opening the menu and for keybinds
	private static readonly bool ControllerSnap = true;      // controller: the right stick jumps between buttons instead of moving a free cursor
	private const float SnapStickThreshold = 0.55f;          // how far the stick must be pushed to jump to the next button (0 to 1)
	private const float SnapReleaseThreshold = 0.30f;        // the stick must come back below this before the next flick counts
	private const int SnapRepeatFirstMs = 350;               // holding the stick: delay before it starts jumping again
	private const int SnapRepeatMs = 200;                    // holding the stick: time between jumps
	private const float SnapConeCos = 0.34f;                 // widest angle a button may be off the stick direction
	private const float SnapSpeed = 28f;                     // how fast the cursor glides to the button (higher = faster, 0 = instant)
	private const int PadChordDelayMs = 80;                  // a button combo must be held this long before it counts (so LB+A isn't read as just A)
	private const int PadDefaultOpenButtons = 0x0002;        // hold this to open the menu: D-pad Down (change it in the menu: top-right button)
	private const string KeybindFileName = "WebWheelVKeybinds.ini";   // saved next to the script, in /scripts
	private const string KeepEngineId = "Keep Engine Running";         // keybind id of the extra box
	private const string AlarmId = "Car Alarm";                        // keybind id of the alarm box
	private const string MenuKeyId = "Menu key";                       // pseudo-id used while choosing the menu key
	private const string MenuKeyFileName = "menu_key.png";             // icon of the top-right "change menu key" button
	private const float CornerButtonX = 600f;                          // top-right button, offset from the screen center
	private const float CornerButtonY = -325f;
	private const float CornerButtonSize = 30f;

	// bottom-right "menu blur" on/off button (same look as the top-right one)
	private const float BlurButtonX = 600f;
	private const float BlurButtonY = 325f;
	private const string BlurFileName = "blur.png";

	// "blinker sounds" on/off button, just above the blur button
	private const float BlinkSoundButtonX = 600f;
	private const float BlinkSoundButtonY = 287f;
	private const string BlinkSoundFileName = "blinker_sound.png";

	// "slow motion while the menu is open" on/off button, just above the blinker sounds button
	private const float SlowMoButtonX = 600f;
	private const float SlowMoButtonY = 249f;
	private const string SlowMoFileName = "slowmo.png";   // in the icon folder; rename to match your icon

	// "permanent seatbelt" button (auto / always fastened / always off)
	private const float PermBeltButtonX = 600f;
	private const float PermBeltButtonY = 211f;
	private const string PermBeltFileName = "perm_seatbelt.png";

	// Turn-signal clicks while an indicator or the hazards are on (tick, tock, tick, ...)
	private static readonly bool BlinkerSounds = true;
	private const int BlinkerIntervalMs = 610;                        // time between a tick and a tock
	private const string BlinkerTickFileName = "blinker_tick.wav";    // in the icon folder
	private const string BlinkerTockFileName = "blinker_tock.wav";

	// Font used for all text in the menu
	private const GTA.UI.Font MenuFont = GTA.UI.Font.Pricedown;
	private const float FontScale = 0.85f;   // multiplies every text size
	private static readonly bool ActivateOnRelease = true; // releasing the key while hovering a button activates it
	private static readonly bool UiSounds = true;
	private const string SoundSet = "HUD_FRONTEND_DEFAULT_SOUNDSET";
	private const string HoverSound = "NAV_LEFT_RIGHT";     // moving onto a button
	private const string SelectSound = "SELECT";            // activating a button
	private const string UnavailableSound = "ERROR";        // activating a button that is N/A
	private static readonly bool BlurBackground = true;    // blur the game behind the menu while it is open
	private const float BlurStrength = 1.0f;     // 0 to 1: how strong the blur gets
	private const float BlurFadeInSec = 0.3f;    // seconds for the blur to fade in (higher = gentler)
	private const float BlurFadeOutSec = 0.3f;   // seconds for the blur to fade out

	// ----- PNG icons -----
	private const string IconFolderName = "WebWheelVIcons";
	private const string CircleFileName = "circle.png";
	private const float IconSize = 38f;         // inner ring PNG size (virtual 1280x720 units)
	private const float OuterIconSize = 28f;    // middle ring PNG size
	private const float ThirdIconSize = 26f;    // outer ring PNG size
	private static readonly bool TintIcons = true;

	// ----- Layout (1280x720 virtual screen space) -----
	private const float InnerRadius = 100f;         // ring 0: body parts
	private const float OuterRadius = 172f;         // ring 1: windows / engine / hazards
	private const float ThirdRadius = 245f;         // ring 2: extras
	private const float ButtonRadius = 29f;         // ring 0 buttons
	private const float OuterButtonRadius = 22f;    // ring 1 buttons
	private const float ThirdButtonRadius = 20f;    // ring 2 buttons
	private const float HoverDistance = 46f;        // cursor must be this close to a button center
	private const float DeadZone = 30f;             // cursor must leave the center to select

	// "Keep engine running" box: a square, outside the rings (offset from the screen center)
	private const float KeepEngineBoxX = 185f;
	private const float KeepEngineBoxY = 0f;
	private const float KeepEngineBoxSize = 40f;
	private const string KeepEngineFileName = "keep_engine.png";   // the icon inside the box

	// "Car alarm" box: a square on the left side, mirrored from the keep-engine box
	private const float AlarmBoxX = -185f;
	private const float AlarmBoxY = 0f;
	private const float AlarmBoxSize = 40f;
	private const string AlarmFileName = "alarm.png";              // the icon inside the box
	private const int AlarmDurationMs = 30000;                     // how long a manually started alarm sounds
	private const string SquareFileName = "square.png";            // the box background (and its hover frame)
	private const int BicycleHelmetDrawable = -1;
	private const string HelmetFileName = "helmet.png";           // icon shown instead of seatbelt.png on bikes
	private static readonly string[][] HelmetAnims =
	{
		new[] { "anim@mp_helmets@on_foot", "put_on_helmet", "take_off_helmet" },
		new[] { "veh@common@fp_helmet@",   "put_on_helmet", null },
		new[] { "mp_masks@on_foot",        "put_on_mask",   "take_off_mask" }
	};
	private static readonly int[] HelmetAnimFlags = { 48, 49 };   // 48 = upper body + secondary slot, 49 = same, looping
	private const float HelmetAnimMaxSpeed = 2f;      // m/s: faster than this, no animation
	private const float HelmetApplyFraction = 0.55f;  // the helmet appears / disappears this far into the animation (0-1)
	private const float RoofInstantSpeed = 3f;   // m/s: above this the roof is switched instantly (animation would be cancelled)
	private const int RoofSettleMaxMs = 1500;    // after an instant roof switch, keep re-applying it for up to this long until the roof really reaches the target state
	private const int RoofRetryMs = 120;         // delay between those re-applications

	// Faint cobweb behind the buttons (one picture, centered on the menu)
	private static readonly bool ShowWeb = true;
	private const int WebAlpha = 60;                      // 0-255: how visible the web is (lower = fainter)
	private const float WebSize = 600f;                   // width/height of the cobweb, virtual 1280x720 units
	private const string WebFileName = "cobweb.png";

	private enum Kind
	{
		Door, Window, Engine, Hazard,
		Roof, InteriorLight, Headlights, IndicatorLeft, IndicatorRight,
		DoorLocks, Radio, Seatbelt, Neon, Siren
	}

	private class Entry
	{
		public string Id;           // used in the keybind file (same as the label)
		public string Label;
		public string Short;        // text shown when there is no PNG for this button
		public Kind Kind;
		public int Index;           // door: VehicleDoorIndex value, window: 0=FL 1=FR 2=RL 3=RR
		public float X, Y;          // offset from the screen center
		public string IconFile;     // PNG file name inside the icon folder
		public CustomSprite Icon;   // null = show the short text label instead
		public CustomSprite[] ModeIcons;   // buttons with one icon per state: headlights (auto, low, high, off), siren (off, lights only, lights + sound)
		public float Radius;        // button circle radius
		public float IconPx;        // PNG icon size
		public CustomSprite Bg;     // circle sprite for the button background
		public CustomSprite Ring;   // circle sprite for the hover ring
	}

	private readonly List<Entry> entries = new List<Entry>();

	private CustomSprite webSprite;

	// Circle sprites for the cursor (the shared circle.png, one instance per use)
	private CustomSprite cursorOuter, cursorInner;

	// Script-tracked states
	private readonly HashSet<int> windowsDown = new HashSet<int>();   // handle * 8 + window index
	private readonly HashSet<int> hazardsOn = new HashSet<int>();
	private readonly HashSet<int> leftOn = new HashSet<int>();
	private readonly HashSet<int> rightOn = new HashSet<int>();
	private readonly HashSet<int> interiorOn = new HashSet<int>();
	private readonly HashSet<int> radioOff = new HashSet<int>();
	private readonly HashSet<int> mutedSirens = new HashSet<int>();
	private readonly Dictionary<int, int> headlightMode = new Dictionary<int, int>();   // 0 auto, 1 low, 2 high, 3 off
	private readonly Dictionary<int, string> lastStation = new Dictionary<int, string>();

	private bool menuOpen;
	private bool clickedThisHold;   // a button was already clicked during the current hold
	private bool boxHovered;        // the cursor is over the "keep engine running" box
	private bool alarmHovered;      // the cursor is over the "car alarm" box
	private int lastHover = -1;     // what was hovered last frame (-1 nothing, -2 the box, -3 the corner button), for the hover sound
	private bool cornerHovered;     // the cursor is over the top-right "change menu key" button
	private bool blurHovered;       // the cursor is over the bottom-right "menu blur" button
	private bool blurEnabled = true;   // player setting (saved in the ini as MenuBlur)
	private bool blinkSoundHovered;    // the cursor is over the "blinker sounds" button
	private bool blinkSoundEnabled = true;   // player setting (saved in the ini as BlinkerSound)
	private bool slowMoHovered;        // the cursor is over the "slow motion" button
	private bool slowMoEnabled = false;   // player setting (saved in the ini as MenuSlowMo)
	private bool permBeltHovered;      // the cursor is over the "permanent seatbelt" button
	private int permBeltMode;          // 0 = auto (game default), 1 = always fastened, 2 = always off (saved in the ini as PermSeatbelt)
	private bool permBeltForced;       // the script is currently overriding the seatbelt flag
	private bool openSuppressed;    // the menu key was just changed: ignore it until it is released
	private CustomSprite menuKeyIcon;
	private CustomSprite blurIcon;
	private CustomSprite blinkSoundIcon;
	private CustomSprite slowMoIcon;
	private CustomSprite permBeltIcon;
	private System.Media.SoundPlayer blinkerTick, blinkerTock;
	private readonly string[][] helmetAnimUsed = new string[2][];   // { dict, clip }
	private readonly bool[] helmetAnimReverse = new bool[2];
	private readonly int[] helmetAnimFlag = { 48, 48 };
	private int helmetApplyAt;        // game time when the helmet change happens (0 = nothing pending)
	private bool helmetApplyOn;
	private int helmetApplyVeh;
	private bool blinkerActive, blinkerPhase;
	private int blinkerNextTime;

	// Keybinds
	private readonly Dictionary<string, Keys> bindings = new Dictionary<string, Keys>();   // button id -> key
	private readonly HashSet<Keys> heldKeys = new HashSet<Keys>();    // keys currently down (ignores auto-repeat)
	private string bindingId;       // button waiting for a key (null = not assigning)
	private string bindingNotice;   // short message shown while assigning
	private bool openKeyHeld;       // the menu key is physically down

	// Controller: XInput button bits (0x0001 D-pad up, 0x0002 down, 0x0004 left, 0x0008 right, 0x0040 L3, 0x0080 R3,
	// 0x0100 LB, 0x0200 RB, 0x1000 A, 0x2000 B, 0x4000 X, 0x8000 Y). LT and RT are not used: LT = right-click, RT = click in the menu.
	private readonly Dictionary<string, int> padBindings = new Dictionary<string, int>();   // button id -> button combo
	private int padOpenMask = PadDefaultOpenButtons;   // buttons held to open the menu (saved as MenuPad)
	private bool usingPad;            // the controller was used more recently than the keyboard (decides which keys are shown)
	private bool padOpenHeld;         // the open button(s) are down
	private bool menuOpenedByPad;     // the menu was opened with the controller (so releasing the pad button closes it)
	private bool padBlock;            // ignore keybinds until every button is let go (after the menu closed / opened)
	private bool padClickNow;         // A was pressed inside the menu this frame
	private bool padBackPrev;
	private int padLast, padChangeAt;
	private bool padFired;
	private int padCapturePeak;       // buttons seen while assigning a controller button
	private readonly int[] padProbeAt = new int[4];
	private int padApi;               // 0 = not tried, 1 = xinput1_4, 2 = xinput9_1_0, 3 = none
	private float padStickX, padStickY;   // right stick, -1 to 1 (up is positive, like XInput)
	private bool snapMode;                // the controller is steering the cursor (jump between buttons)
	private float snapX, snapY;           // the button the cursor is on / gliding to (offset from the screen center)
	private bool snapStickDown;           // the stick was pushed past the threshold last tick
	private int snapNextAt;               // earliest real time (ms) for the next jump while the stick is held
	private string feedbackId;      // keybind just used: show its new state shortly
	private int feedbackAt;

	// Keep-engine-running state
	private readonly HashSet<int> keepEngineOn = new HashSet<int>();   // vehicles set to keep running
	private CustomSprite keepEngineIcon;
	private CustomSprite alarmIcon;
	private CustomSprite boxBg, boxFrame;   // square.png sprites for the box background / hover frame
	private int keepVehicle;        // the vehicle the player was last in
	private int runningStamp;       // game time the engine was last seen running while inside
	private bool exitLatched;       // the exit of keepVehicle has been handled
	private bool keepReassert;      // keep forcing the engine on for a few seconds after exiting
	private float keepTimer;
	private bool loadedMessageShown;
	private float cursorX, cursorY;
	private int hovered = -1;

	public VehicleRadialMenu()
	{
		// Ring 0 (angle: 0 = top, clockwise). Door indexes: 0=FL 1=FR 2=RL 3=RR 4=Hood 5=Boot
		AddEntry("Hood",        "HOOD",  Kind.Door, 4, 0f,   0);
		AddEntry("Front Right", "FR",    Kind.Door, 1, 60f,  0);
		AddEntry("Rear Right",  "RR",    Kind.Door, 3, 120f, 0);
		AddEntry("Boot",        "BOOT",  Kind.Door, 5, 180f, 0);
		AddEntry("Rear Left",   "RL",    Kind.Door, 2, 240f, 0);
		AddEntry("Front Left",  "FL",    Kind.Door, 0, 300f, 0);

		// Ring 1
		AddEntry("Engine",      "ENG",   Kind.Engine, 0, 0f,   1);
		AddEntry("Window FR",   "WFR",   Kind.Window, 1, 60f,  1);
		AddEntry("Window RR",   "WRR",   Kind.Window, 3, 120f, 1);
		AddEntry("Hazards",     "HAZ",   Kind.Hazard, 0, 180f, 1);
		AddEntry("Window RL",   "WRL",   Kind.Window, 2, 240f, 1);
		AddEntry("Window FL",   "WFL",   Kind.Window, 0, 300f, 1);

		// Ring 2 (10 buttons, 36 degrees apart)
		AddEntry("Headlights",       "BEAM",  Kind.Headlights,     0, 0f,   2);
		AddEntry("Door Locks",       "LOCK",  Kind.DoorLocks,      0, 36f,  2);
		AddEntry("Right Indicator",  ">",     Kind.IndicatorRight, 0, 72f,  2);
		AddEntry("Seatbelt",         "BELT",  Kind.Seatbelt,       0, 108f, 2);
		AddEntry("Interior Light",   "LAMP",  Kind.InteriorLight,  0, 144f, 2);
		AddEntry("Convertible Roof", "ROOF",  Kind.Roof,           0, 180f, 2);
		AddEntry("Radio",            "RADIO", Kind.Radio,          0, 216f, 2);
		AddEntry("Siren",            "SIREN", Kind.Siren,          0, 252f, 2);
		AddEntry("Left Indicator",   "<",     Kind.IndicatorLeft,  0, 288f, 2);
		AddEntry("Neon Lights",      "NEON",  Kind.Neon,           0, 324f, 2);

		LoadBindings();

		Tick += OnTick;
		KeyDown += OnKeyDown;
		KeyUp += OnKeyUp;
		Aborted += (s, e) => { CloseMenu(); ResetBlur(); RestoreNeons(); };
	}

	private void AddEntry(string label, string shortText, Kind kind, int index, float angleDeg, int ring)
	{
		float radius = ring == 0 ? InnerRadius : (ring == 1 ? OuterRadius : ThirdRadius);
		float buttonRadius = ring == 0 ? ButtonRadius : (ring == 1 ? OuterButtonRadius : ThirdButtonRadius);
		float iconPx = ring == 0 ? IconSize : (ring == 1 ? OuterIconSize : ThirdIconSize);

		double rad = angleDeg * Math.PI / 180.0;
		entries.Add(new Entry
		{
			Id = label,
			Label = label,
			Short = shortText,
			Kind = kind,
			Index = index,
			X = (float)Math.Sin(rad) * radius,
			Y = -(float)Math.Cos(rad) * radius,
			IconFile = GetIconFileName(kind, index),
			Radius = buttonRadius,
			IconPx = iconPx
		});
	}

	private static string GetIconFileName(Kind kind, int index)
	{
		switch (kind)
		{
			case Kind.Door:
				switch (index)
				{
					case 0: return "door_front_left.png";
					case 1: return "door_front_right.png";
					case 2: return "door_rear_left.png";
					case 3: return "door_rear_right.png";
					case 4: return "hood.png";
					default: return "boot.png";
				}

			case Kind.Window:
				switch (index)
				{
					case 0: return "window_front_left.png";
					case 1: return "window_front_right.png";
					case 2: return "window_rear_left.png";
					default: return "window_rear_right.png";
				}

			case Kind.Engine: return "engine.png";
			case Kind.Hazard: return "hazards.png";
			case Kind.Roof: return "roof.png";
			case Kind.InteriorLight: return "interior_light.png";
			case Kind.Headlights: return "headlights.png";
			case Kind.IndicatorLeft: return "indicator_left.png";
			case Kind.IndicatorRight: return "indicator_right.png";
			case Kind.DoorLocks: return "door_locks.png";
			case Kind.Radio: return "radio.png";
			case Kind.Seatbelt: return "seatbelt.png";
			case Kind.Neon: return "neon.png";
			default: return "siren.png";
		}
	}

	private static readonly Dictionary<string, Hash> hashCache = new Dictionary<string, Hash>();

	private static Hash N(params string[] names)
	{
		Hash h;
		if (hashCache.TryGetValue(names[0], out h)) return h;

		foreach (string name in names)
		{
			try
			{
				h = (Hash)Enum.Parse(typeof(Hash), name);
				hashCache[names[0]] = h;
				return h;
			}
			catch (Exception)
			{
				// try the next candidate name
			}
		}

		throw new InvalidOperationException("Native not found: " + names[0]);
	}

	private static void Debug(string msg)
	{
		if (DebugMessages) Screen.ShowSubtitle("[WebWheelV] " + msg, 2500);
	}

	// ------------------------------------------------------------------
	// Icon loading
	// ------------------------------------------------------------------

	private int LoadIcons()
	{
		string folder = Path.Combine(Directory.GetCurrentDirectory(), "scripts", IconFolderName);
		int loaded = 0;

		foreach (Entry e in entries)
		{
			string path = Path.Combine(folder, e.IconFile);
			if (!File.Exists(path)) continue;

			try
			{
				e.Icon = new CustomSprite(path, new SizeF(e.IconPx, e.IconPx),
										  new PointF(0f, 0f), Color.White, 0f, true);
				loaded++;
			}
			catch (Exception)
			{
				e.Icon = null;
			}
		}

		// Buttons with one icon per state
		//   headlights: 0 = auto, 1 = low beam, 2 = high beam, 3 = off
		//   siren:      0 = off,  1 = lights only, 2 = lights + sound
		string[] headlightFiles =
		{
			"headlights_auto.png", "headlights_low_beam.png", "headlights_high_beam.png", "headlights_off.png"
		};
		string[] sirenFiles =
		{
			"siren_off.png", "siren_lights_only.png", "siren_on.png"
		};
		string[] seatbeltFiles = { "seatbelt.png", HelmetFileName };   // 0 = car (seatbelt), 1 = bike (helmet)

		foreach (Entry e in entries)
		{
			string[] stateFiles = e.Kind == Kind.Headlights ? headlightFiles
								: e.Kind == Kind.Siren ? sirenFiles
								: e.Kind == Kind.Seatbelt ? seatbeltFiles : null;
			if (stateFiles == null) continue;

			e.ModeIcons = new CustomSprite[stateFiles.Length];
			for (int m = 0; m < stateFiles.Length; m++)
			{
				string modePath = Path.Combine(folder, stateFiles[m]);
				if (!File.Exists(modePath)) continue;

				try
				{
					e.ModeIcons[m] = new CustomSprite(modePath, new SizeF(e.IconPx, e.IconPx),
													  new PointF(0f, 0f), Color.White, 0f, true);
				}
				catch (Exception)
				{
					e.ModeIcons[m] = null;
				}
			}

			// No plain icon (headlights.png / siren.png): borrow a state icon so the button still counts
			// as having a PNG (smooth circle background) and has something to fall back on for a missing state.
			if (e.Icon == null && e.Kind != Kind.Seatbelt)
			{
				foreach (CustomSprite candidate in e.ModeIcons)
				{
					if (candidate != null)
					{
						e.Icon = candidate;
						loaded++;
						break;
					}
				}
			}
		}

		string keyPath = Path.Combine(folder, KeepEngineFileName);
		if (File.Exists(keyPath)) keepEngineIcon = MakeSprite(keyPath);

		string alarmPath = Path.Combine(folder, AlarmFileName);
		if (File.Exists(alarmPath)) alarmIcon = MakeSprite(alarmPath);

		string menuKeyPath = Path.Combine(folder, MenuKeyFileName);
		if (File.Exists(menuKeyPath)) menuKeyIcon = MakeSprite(menuKeyPath);

		string blurPath = Path.Combine(folder, BlurFileName);
		if (File.Exists(blurPath)) blurIcon = MakeSprite(blurPath);

		string blinkSoundPath = Path.Combine(folder, BlinkSoundFileName);
		if (File.Exists(blinkSoundPath)) blinkSoundIcon = MakeSprite(blinkSoundPath);

		string slowMoPath = Path.Combine(folder, SlowMoFileName);
		if (File.Exists(slowMoPath)) slowMoIcon = MakeSprite(slowMoPath);

		string permBeltPath = Path.Combine(folder, PermBeltFileName);
		if (!File.Exists(permBeltPath)) permBeltPath = Path.Combine(folder, "seatbelt.png");
		if (File.Exists(permBeltPath)) permBeltIcon = MakeSprite(permBeltPath);

		string squarePath = Path.Combine(folder, SquareFileName);
		if (File.Exists(squarePath))
		{
			boxBg = MakeSprite(squarePath);
			boxFrame = MakeSprite(squarePath);
		}

		// Circle sprites (one instance per use so they can be drawn several times per frame)
		string circlePath = Path.Combine(folder, CircleFileName);
		if (File.Exists(circlePath))
		{
			foreach (Entry e in entries)
			{
				e.Bg = MakeSprite(circlePath);
				e.Ring = MakeSprite(circlePath);
			}
			cursorOuter = MakeSprite(circlePath);
			cursorInner = MakeSprite(circlePath);
		}

		blinkerTick = LoadWav(Path.Combine(folder, BlinkerTickFileName));
		blinkerTock = LoadWav(Path.Combine(folder, BlinkerTockFileName));

		webSprite = null;
		string webPath = Path.Combine(folder, WebFileName);
		if (ShowWeb && File.Exists(webPath)) webSprite = MakeSprite(webPath);

		return loaded;
	}

	// ------------------------------------------------------------------
	// Cobweb background
	// ------------------------------------------------------------------
	private void DrawWeb(float cx, float cy)
	{
		if (webSprite == null) return;

		try
		{
			webSprite.Position = new PointF(cx, cy);
			webSprite.Size = new SizeF(WebSize, WebSize);
			webSprite.Centered = true;
			webSprite.Color = Color.FromArgb(WebAlpha, 255, 255, 255);
			webSprite.Draw();
		}
		catch (Exception)
		{
			webSprite = null;
		}
	}

	private static CustomSprite MakeSprite(string path)
	{
		try
		{
			return new CustomSprite(path, new SizeF(10f, 10f), new PointF(0f, 0f), Color.White, 0f, true);
		}
		catch (Exception)
		{
			return null;
		}
	}

	// Draws the PNG for a button. Returns false if there is none (or it failed).
	// stateIndex picks the icon for buttons that have one per state (headlights); -1 = the normal icon.
	private bool TryDrawPngIcon(Entry e, float x, float y, Color tint, bool available, int stateIndex)
	{
		CustomSprite sprite = e.Icon;
		bool usingStateIcon = false;

		if (stateIndex >= 0 && e.ModeIcons != null && stateIndex < e.ModeIcons.Length &&
			e.ModeIcons[stateIndex] != null)
		{
			sprite = e.ModeIcons[stateIndex];
			usingStateIcon = true;
		}

		if (sprite == null) return false;

		try
		{
			sprite.Position = new PointF(x, y);
			sprite.Size = new SizeF(e.IconPx, e.IconPx);
			sprite.Centered = true;
			sprite.Color = TintIcons ? tint : Color.FromArgb(available ? 255 : 90, 255, 255, 255);
			sprite.Draw();
			return true;
		}
		catch (Exception ex)
		{
			// show the text label from now on
			if (usingStateIcon) e.ModeIcons[stateIndex] = null; else e.Icon = null;
			Debug("PNG draw failed: " + ex.Message);
			return false;
		}
	}

	// Draws a filled circle using a circle.png sprite (smooth edges, no draw-rect budget used).
	private static void DrawSmoothCircle(CustomSprite sprite, float cx, float cy, float r, Color c)
	{
		if (sprite == null) return;

		try
		{
			sprite.Position = new PointF(cx, cy);
			sprite.Size = new SizeF(r * 2f, r * 2f);
			sprite.Centered = true;
			sprite.Color = c;
			sprite.Draw();
		}
		catch (Exception)
		{
			// ignore
		}
	}

	// ------------------------------------------------------------------
	// Input
	// ------------------------------------------------------------------
	private void OnKeyDown(object sender, KeyEventArgs e)
	{
		usingPad = false;
		if (IsShiftKey(e.KeyCode)) shiftTracked = true;

		// Waiting for the player to press the key to assign
		if (bindingId != null)
		{
			heldKeys.Add(e.KeyCode);   // so this key's auto-repeat isn't treated as a new press afterwards
			HandleBindingKey(BuildKeyData(e));
			return;
		}

		bool firstPress = heldKeys.Add(e.KeyCode);   // false for the auto-repeat while a key is held

		if (e.KeyCode == OpenKey)
		{
			if (openSuppressed) return;   // the menu key was just changed and is still held

			openKeyHeld = true;
			if (menuOpen) return;

			Debug(OpenKey + " key detected");

			Ped player = Game.Player.Character;
			if (player != null && player.Exists() && player.IsInVehicle())
			{
				Debug("In vehicle - opening menu");
				OpenMenu();
			}
			else
			{
				Debug("You must be inside a vehicle");
			}
			return;
		}

		// A key assigned to a button
		if (firstPress && !menuOpen) RunKeybind(BuildKeyData(e));   // key + any held Ctrl/Shift/Alt
	}

	private void OnKeyUp(object sender, KeyEventArgs e)
	{
		if (IsShiftKey(e.KeyCode))
		{
			shiftTracked = false;
			shiftUpUtc = DateTime.UtcNow;
		}

		heldKeys.Remove(e.KeyCode);
		if (e.KeyCode == OpenKey)
		{
			openKeyHeld = false;
			openSuppressed = false;
		}

		if (e.KeyCode == OpenKey && menuOpen && !menuOpenedByPad) OnOpenReleased();
	}

	// The menu key (or the controller open button) was let go while the menu is open:
	// activate the hovered button and close.
	private void OnOpenReleased()
	{
		{
			// While a key is being assigned the menu stays open; it closes when that finishes
			if (bindingId != null) return;

			// Releasing the key over the top-right button asks for a new menu key
			if (ActivateOnRelease && cornerHovered && !clickedThisHold)
			{
				PlayUiSound(SelectSound);
				StartBinding(MenuKeyId);
				return;
			}

			// Releasing the key over a bottom-right button toggles that setting
			if (ActivateOnRelease && blurHovered && !clickedThisHold)
			{
				MenuToggleBlur();
			}
			else if (ActivateOnRelease && blinkSoundHovered && !clickedThisHold)
			{
				MenuToggleBlinkSound();
			}
			else if (ActivateOnRelease && slowMoHovered && !clickedThisHold)
			{
				MenuToggleSlowMo();
			}
			else if (ActivateOnRelease && permBeltHovered && !clickedThisHold)
			{
				MenuCyclePermBelt();
			}

			// Releasing the key over a button activates it, just like a click.
			// Skipped if the player already clicked during this hold, to avoid toggling twice.
			if (ActivateOnRelease && (hovered >= 0 || boxHovered || alarmHovered) && !clickedThisHold)
			{
				Vehicle veh = Game.Player.Character.CurrentVehicle;
				if (veh != null && veh.Exists())
				{
					if (boxHovered) MenuToggleKeepEngine(veh);
					else if (alarmHovered) MenuToggleAlarm(veh);
					else MenuToggle(veh, entries[hovered]);
				}
			}

			Debug(OpenKey + " released - closing menu");
			CloseMenu();
		}
	}

	private void OpenMenu(bool byPad = false)
	{
		menuOpen = true;
		menuOpenedByPad = byPad;
		padBlock = true;
		clickedThisHold = false;
		boxHovered = false;
		alarmHovered = false;
		cornerHovered = false;
		blurHovered = false;
		blinkSoundHovered = false;
		slowMoHovered = false;
		permBeltHovered = false;
		lastHover = -1;
		cursorX = 0f;
		cursorY = 0f;
		snapMode = false;
		snapX = 0f;
		snapY = 0f;
		snapStickDown = false;
		snapNextAt = 0;
		hovered = -1;
		Game.TimeScale = (slowMoEnabled && SlowMoScale < 1f) ? SlowMoScale : 1f;
	}

	private void CloseMenu()
	{
		if (!menuOpen) return;
		menuOpen = false;
		menuOpenedByPad = false;
		padBlock = true;
		padCapturePeak = 0;
		bindingId = null;
		bindingNotice = null;
		Game.TimeScale = 1f;
	}

	private float blurProgress;        // 0 = no blur, 1 = full blur
	private bool blurModifierSet;

	private void UpdateBlur()
	{
		if (!BlurBackground) return;

		bool wantBlur = menuOpen && blurEnabled;
		float target = wantBlur ? 1f : 0f;
		if (blurProgress == target) return;

		float dt = Game.LastFrameTime;
		if (dt <= 0f) dt = 0.016f;
		if (dt > 0.1f) dt = 0.1f;

		float fade = wantBlur ? BlurFadeInSec : BlurFadeOutSec;
		float step = fade <= 0f ? 1f : dt / fade;

		blurProgress = wantBlur ? Math.Min(1f, blurProgress + step)
								: Math.Max(0f, blurProgress - step);
		ApplyBlur();
	}

	private void ApplyBlur()
	{
		try
		{
			if (blurProgress > 0f)
			{
				if (!blurModifierSet)
				{
					Function.Call(N("SET_TIMECYCLE_MODIFIER"), "hud_def_blur");
					blurModifierSet = true;
				}

				// smoothstep easing so it starts and ends gently
				float eased = blurProgress * blurProgress * (3f - 2f * blurProgress);
				Function.Call(N("SET_TIMECYCLE_MODIFIER_STRENGTH"), eased * BlurStrength);
			}
			else if (blurModifierSet)
			{
				Function.Call(N("CLEAR_TIMECYCLE_MODIFIER"));
				blurModifierSet = false;
			}
		}
		catch (Exception)
		{
			// timecycle natives unavailable: carry on without a blur
		}
	}

	// ------------------------------------------------------------------
	// Turn-signal sounds
	// ------------------------------------------------------------------
	private static System.Media.SoundPlayer LoadWav(string path)
	{
		try
		{
			if (!File.Exists(path)) return null;
			System.Media.SoundPlayer player = new System.Media.SoundPlayer(path);
			player.Load();
			return player;
		}
		catch (Exception)
		{
			return null;
		}
	}

	private void UpdateBlinkerSound()
	{
		if (!BlinkerSounds || !blinkSoundEnabled || blinkerTick == null)
		{
			blinkerActive = false;
			return;
		}

		Vehicle veh = Game.Player.Character.CurrentVehicle;
		bool active = false;
		if (veh != null && veh.Exists())
		{
			int h = veh.Handle;
			active = hazardsOn.Contains(h) || leftOn.Contains(h) || rightOn.Contains(h);
		}

		if (!active)
		{
			blinkerActive = false;
			return;
		}

		if (!blinkerActive)
		{
			// just switched on: start with a tick right away
			blinkerActive = true;
			blinkerPhase = true;
			blinkerNextTime = 0;
		}

		if (Game.GameTime < blinkerNextTime) return;

		try
		{
			(blinkerPhase || blinkerTock == null ? blinkerTick : blinkerTock).Play();
		}
		catch (Exception)
		{
			blinkerTick = null;   // can't play: stop trying
		}

		blinkerPhase = !blinkerPhase;
		blinkerNextTime = Game.GameTime + BlinkerIntervalMs;
	}

	private void ResetBlur()
	{
		blurProgress = 0f;
		ApplyBlur();
	}

	// ------------------------------------------------------------------
	// Convertible roof: make sure an instant switch at speed really finishes
	// ------------------------------------------------------------------
	private int roofWatchHandle;
	private bool roofWatchLower;
	private int roofWatchTarget = -1;   // 2 = should end up lowered, 0 = raised, -1 = not watching
	private int roofWatchEndTime;
	private int roofWatchNextTry;

	private void UpdateRoofWatch()
	{
		if (roofWatchTarget < 0) return;

		int h = roofWatchHandle;
		if (!Function.Call<bool>(Hash.DOES_ENTITY_EXIST, h) || Game.GameTime > roofWatchEndTime)
		{
			roofWatchTarget = -1;
			return;
		}

		int roof = Function.Call<int>(N("GET_CONVERTIBLE_ROOF_STATE"), h);
		if (roof == roofWatchTarget)
		{
			roofWatchTarget = -1;   // arrived, done
			return;
		}

		if (Game.GameTime >= roofWatchNextTry)
		{
			roofWatchNextTry = Game.GameTime + RoofRetryMs;
			if (roofWatchLower)
				Function.Call(N("LOWER_CONVERTIBLE_ROOF"), h, true);
			else
				Function.Call(N("RAISE_CONVERTIBLE_ROOF"), h, true);
		}
	}

	// ------------------------------------------------------------------
	// Permanent seatbelt: force the state in every car, or leave it to the game (auto)
	// ------------------------------------------------------------------
	private void UpdatePermanentSeatbelt()
	{
		Ped player = Game.Player.Character;
		Vehicle veh = player.CurrentVehicle;
		bool inCar = veh != null && veh.Exists() && !IsBikeVehicle(veh);   // bikes use the helmet button instead
		int ped = player.Handle;

		if (permBeltMode != 0 && inCar)
		{
			// config flag 32 = "can fly through windscreen": fastened = flag off, always off = flag on
			bool wantFly = permBeltMode == 2;
			if (Function.Call<bool>(N("GET_PED_CONFIG_FLAG"), ped, 32, true) != wantFly)
				Function.Call(N("SET_PED_CONFIG_FLAG"), ped, 32, wantFly);
			permBeltForced = true;
		}
		else if (permBeltForced)
		{
			// auto again, or left the car: back to the game default (not belted)
			Function.Call(N("SET_PED_CONFIG_FLAG"), ped, 32, true);
			permBeltForced = false;
		}
	}

	// ------------------------------------------------------------------
	// Controller (XInput): open the menu, keybinds, assigning buttons
	// ------------------------------------------------------------------
	[StructLayout(LayoutKind.Sequential)]
	private struct XInputGamepad
	{
		public ushort wButtons;
		public byte bLeftTrigger, bRightTrigger;
		public short sThumbLX, sThumbLY, sThumbRX, sThumbRY;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct XInputState
	{
		public uint dwPacketNumber;
		public XInputGamepad Gamepad;
	}

	[DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
	private static extern uint XInputGetState14(uint userIndex, out XInputState state);

	[DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
	private static extern uint XInputGetState91(uint userIndex, out XInputState state);

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

	private static readonly uint ownProcessId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;

	// bindable buttons (shoulders first, so combos read "LB+A")
	private static readonly int[] PadBits =
		{ 0x0100, 0x0200, 0x0040, 0x0080, 0x0001, 0x0002, 0x0004, 0x0008, 0x1000, 0x2000, 0x4000, 0x8000 };
	private static readonly string[] PadConfigNames =
		{ "LB", "RB", "L3", "R3", "DPadUp", "DPadDown", "DPadLeft", "DPadRight", "A", "B", "X", "Y" };
	private static readonly string[] PadShownNames =
		{ "LB", "RB", "L3", "R3", "D-Up", "D-Down", "D-Left", "D-Right", "A", "B", "X", "Y" };
	private const int PadA = 0x1000;
	private const int PadBack = 0x0020;

	private static string PadMaskToConfig(int mask)
	{
		List<string> parts = new List<string>();
		for (int i = 0; i < PadBits.Length; i++)
			if ((mask & PadBits[i]) != 0) parts.Add(PadConfigNames[i]);
		return string.Join("+", parts.ToArray());
	}

	private static string PadMaskName(int mask)
	{
		List<string> parts = new List<string>();
		for (int i = 0; i < PadBits.Length; i++)
			if ((mask & PadBits[i]) != 0) parts.Add(PadShownNames[i]);
		return string.Join("+", parts.ToArray());
	}

	private static bool TryParsePadMask(string text, out int mask)
	{
		mask = 0;
		foreach (string part in text.Split('+'))
		{
			string t = part.Trim();
			int idx = -1;
			for (int i = 0; i < PadConfigNames.Length; i++)
				if (string.Equals(PadConfigNames[i], t, StringComparison.OrdinalIgnoreCase)) idx = i;
			if (idx < 0) return false;
			mask |= PadBits[idx];
		}
		return mask != 0;
	}

	private uint PadGetState(uint index, out XInputState state)
	{
		state = default(XInputState);
		if (padApi == 3) return 1167;   // no XInput on this PC

		try
		{
			if (padApi != 2)
			{
				uint r = XInputGetState14(index, out state);
				padApi = 1;
				return r;
			}
		}
		catch (Exception)
		{
			padApi = 2;   // fall back to the older DLL
		}

		try
		{
			return XInputGetState91(index, out state);
		}
		catch (Exception)
		{
			padApi = 3;
			return 1167;
		}
	}

	// Reads the buttons of every connected controller. Nothing while the game is in the background.
	private int ReadPad(out bool back)
	{
		back = false;
		padStickX = 0f;
		padStickY = 0f;

		IntPtr fg = GetForegroundWindow();
		uint pid;
		if (fg == IntPtr.Zero) return 0;
		GetWindowThreadProcessId(fg, out pid);
		if (pid != ownProcessId) return 0;

		int now = Game.GameTime;
		int buttons = 0;
		for (uint i = 0; i < 4; i++)
		{
			if (now < padProbeAt[i]) continue;

			XInputState st;
			if (PadGetState(i, out st) != 0)
			{
				padProbeAt[i] = now + 1500;   // not connected: look again in a moment
				continue;
			}
			buttons |= st.Gamepad.wButtons;

			float sx = st.Gamepad.sThumbRX / 32767f;
			float sy = st.Gamepad.sThumbRY / 32767f;
			if (sx * sx + sy * sy > padStickX * padStickX + padStickY * padStickY)
			{
				padStickX = sx;
				padStickY = sy;
			}
		}

		back = (buttons & PadBack) != 0;

		int bindable = 0;
		for (int i = 0; i < PadBits.Length; i++) bindable |= PadBits[i];
		return buttons & bindable;
	}

	private void UpdatePad()
	{
		if (!ControllerSupport) return;

		bool back;
		int mask = ReadPad(out back);
		if (Function.Call<bool>(Hash.IS_PAUSE_MENU_ACTIVE))
		{
			mask = 0;
			back = false;
		}

		int now = Game.GameTime;
		bool wasOpen = menuOpen;

		int prev = padLast;
		if (mask != padLast)
		{
			padLast = mask;
			padChangeAt = now;
			padFired = false;
		}
		int pressed = mask & ~prev;   // buttons that just went down

		bool backPressed = back && !padBackPrev;
		padBackPrev = back;

		if (mask != 0 || back) usingPad = true;
		if (mask == 0) padBlock = false;

		// A clicks the hovered button inside the menu
		if (wasOpen && bindingId == null && (pressed & PadA) != 0) padClickNow = true;

		// the open button(s)
		bool openNow = padOpenMask != 0 && (mask & padOpenMask) == padOpenMask;
		bool openPressed = openNow && !padOpenHeld;
		bool openReleased = !openNow && padOpenHeld;
		padOpenHeld = openNow;

		if (!menuOpen)
		{
			if (openPressed && !padBlock && !openSuppressed)
			{
				Ped player = Game.Player.Character;
				if (player != null && player.Exists() && player.IsInVehicle()) OpenMenu(true);
			}
		}
		else if (menuOpenedByPad && openReleased)
		{
			OnOpenReleased();
		}

		// assigning a controller button / combo
		if (menuOpen && bindingId != null)
		{
			UpdatePadBinding(mask, backPressed);
			return;
		}
		padCapturePeak = 0;

		// keybinds on the controller
		if (!menuOpen && mask != 0 && !padFired && !padBlock && !openNow &&
			padBindings.Count > 0 && now - padChangeAt >= PadChordDelayMs)
		{
			padFired = true;
			RunPadBind(mask);
		}
	}

	// While a button is being assigned: take what is pressed, and use it once everything is let go
	// (so a combo like LB+A is read as a whole).
	private void UpdatePadBinding(int mask, bool backPressed)
	{
		bool forMenuKey = bindingId == MenuKeyId;

		// the menu button still held from opening the menu is not part of the new choice
		int cap = forMenuKey ? mask : (mask & ~padOpenMask);

		if (backPressed && !forMenuKey)
		{
			string label = LabelOf(bindingId);
			padBindings.Remove(bindingId);
			SaveBindings();
			padCapturePeak = 0;
			padBlock = true;
			EndBinding(label + ": button cleared");
			return;
		}

		// start listening only once the buttons that started this are let go
		if (!padCaptureArmed)
		{
			if (cap == 0 && (!forMenuKey || mask == 0)) padCaptureArmed = true;
			else return;
		}

		if (cap != 0)
		{
			padCapturePeak |= cap;
			return;
		}

		if (padCapturePeak == 0) return;

		int chosen = padCapturePeak;
		padCapturePeak = 0;
		padCaptureArmed = false;
		if (forMenuKey) HandlePadMenuChoice(chosen); else HandlePadBinding(chosen);
	}

	private bool padCaptureArmed;

	private void HandlePadBinding(int chosen)
	{
		// a combo that includes the open button(s) would open the menu instead
		if (padOpenMask != 0 && (chosen & padOpenMask) == padOpenMask)
		{
			bindingNotice = "That opens the menu";
			return;
		}

		// a combo can only belong to one button
		List<string> clashes = new List<string>();
		foreach (KeyValuePair<string, int> kv in padBindings)
			if (kv.Value == chosen && kv.Key != bindingId) clashes.Add(kv.Key);
		foreach (string c in clashes) padBindings.Remove(c);

		string label = LabelOf(bindingId);
		padBindings[bindingId] = chosen;
		SaveBindings();
		padBlock = true;
		EndBinding(label + " bound to " + PadMaskName(chosen));
	}

	private void HandlePadMenuChoice(int chosen)
	{
		foreach (KeyValuePair<string, int> kv in padBindings)
		{
			// pressing the new open button would always open the menu before this combo could finish
			if ((kv.Value & chosen) == chosen)
			{
				bindingNotice = "Already used by: " + kv.Key;
				return;
			}
		}

		padOpenMask = chosen;
		padOpenHeld = false;
		padBlock = true;
		SaveBindings();
		EndBinding("Menu button set to " + PadMaskName(chosen));
	}

	// ------------------------------------------------------------------
	// Main loop
	// ------------------------------------------------------------------
	// ----- Auto-unlock when the player leaves the vehicle -----
	private static readonly bool AutoUnlockOnExit = true;
	private int lastOccupiedHandle;

	private void WatchAutoUnlock()
	{
		if (!AutoUnlockOnExit) return;

		Ped player = Game.Player.Character;
		if (player == null || !player.Exists()) return;

		if (player.IsInVehicle())
		{
			Vehicle v = player.CurrentVehicle;
			lastOccupiedHandle = (v != null && v.Exists()) ? v.Handle : 0;
			return;
		}

		// on foot: if we were in a vehicle a moment ago, unlock it once
		if (lastOccupiedHandle == 0) return;

		int h = lastOccupiedHandle;
		lastOccupiedHandle = 0;

		if (!Function.Call<bool>(Hash.DOES_ENTITY_EXIST, h)) return;

		// only undo the plain lock this script sets (2); leave mission or special lock states alone
		if (Function.Call<int>(N("GET_VEHICLE_DOOR_LOCK_STATUS"), h) == 2)
			Function.Call(N("SET_VEHICLE_DOORS_LOCKED"), h, 1);
	}

	private void OnTick(object sender, EventArgs e)
	{
		if (!loadedMessageShown)
		{
			loadedMessageShown = true;
			int icons = LoadIcons();
			Debug("Script loaded! PNG icons found: " + icons + "/" + entries.Count + ". Hold " + OpenKey + " in a vehicle.");
		}

		UpdateBlur();
		UpdateBlinkerSound();
		WatchHeadlightKey();
		WatchKeepEngine();
		ShowPendingFeedback();
		UpdateHelmetChange();
		UpdateRoofWatch();
		UpdatePermanentSeatbelt();
		UpdatePad();
		WatchAutoUnlock();

		if (!menuOpen) return;

		Ped player = Game.Player.Character;
		Vehicle veh = player.CurrentVehicle;

		if (veh == null || !veh.Exists() || player.IsDead)
		{
			CloseMenu();
			return;
		}

		DisableGameplayControls();
		UpdateCursor();
		UpdateHoverSound();
		DrawMenu(veh);

		bool padClick = padClickNow;   // A on the controller
		padClickNow = false;

		bool rightClick = Function.Call<bool>(Hash.IS_DISABLED_CONTROL_JUST_PRESSED, 0, (int)Control.Aim) ||
						  Function.Call<bool>(Hash.IS_DISABLED_CONTROL_JUST_PRESSED, 0, (int)Control.VehicleAim);

		if (rightClick)
		{
			// Right-click a button to assign a key; right-click again to cancel
			if (bindingId != null) EndBinding(null);
			else if (cornerHovered) StartBinding(MenuKeyId);
			else if (boxHovered) StartBinding(KeepEngineId);
			else if (alarmHovered) StartBinding(AlarmId);
			else if (hovered >= 0) StartBinding(entries[hovered].Id);
		}
		else if (bindingId == null && (hovered >= 0 || boxHovered || alarmHovered || cornerHovered || blurHovered || blinkSoundHovered || slowMoHovered || permBeltHovered) &&
				 (padClick || Function.Call<bool>(Hash.IS_DISABLED_CONTROL_JUST_PRESSED, 0, (int)Control.Attack)))
		{
			clickedThisHold = true;
			if (cornerHovered)
			{
				PlayUiSound(SelectSound);
				StartBinding(MenuKeyId);
			}
			else if (blurHovered) MenuToggleBlur();
			else if (blinkSoundHovered) MenuToggleBlinkSound();
			else if (slowMoHovered) MenuToggleSlowMo();
			else if (permBeltHovered) MenuCyclePermBelt();
			else if (boxHovered) MenuToggleKeepEngine(veh);
			else if (alarmHovered) MenuToggleAlarm(veh);
			else MenuToggle(veh, entries[hovered]);
		}
	}

	private void DisableGameplayControls()
	{
		Game.DisableControlThisFrame(Control.LookLeftRight);
		Game.DisableControlThisFrame(Control.LookUpDown);
		Game.DisableControlThisFrame(Control.Attack);
		Game.DisableControlThisFrame(Control.Attack2);
		Game.DisableControlThisFrame(Control.Aim);
		Game.DisableControlThisFrame(Control.VehicleAttack);
		Game.DisableControlThisFrame(Control.VehicleAttack2);
		Game.DisableControlThisFrame(Control.VehicleAim);
		Game.DisableControlThisFrame(Control.VehicleMouseControlOverride);
		Game.DisableControlThisFrame(Control.VehicleLookLeft);
		Game.DisableControlThisFrame(Control.VehicleLookRight);
		Game.DisableControlThisFrame(Control.WeaponWheelNext);
		Game.DisableControlThisFrame(Control.WeaponWheelPrev);
		Game.DisableControlThisFrame(Control.SelectWeapon);

		// controller buttons used by the menu must not also drive the car
		Game.DisableControlThisFrame(Control.VehicleHandbrake);
		Game.DisableControlThisFrame(Control.VehicleExit);
		Game.DisableControlThisFrame(Control.VehicleHorn);
		Game.DisableControlThisFrame(Control.VehicleLookBehind);
		Game.DisableControlThisFrame(Control.VehicleCinCam);
		Game.DisableControlThisFrame(Control.VehicleRadioWheel);
		Game.DisableControlThisFrame(Control.VehicleHeadlight);
		Game.DisableControlThisFrame(Control.Phone);
	}

	// ----- Controller snapping -----
	private List<PointF> GetSnapTargets()
	{
		List<PointF> t = new List<PointF>();
		t.Add(new PointF(0f, 0f));
		for (int i = 0; i < entries.Count; i++) t.Add(new PointF(entries[i].X, entries[i].Y));
		t.Add(new PointF(KeepEngineBoxX, KeepEngineBoxY));
		t.Add(new PointF(AlarmBoxX, AlarmBoxY));
		t.Add(new PointF(CornerButtonX, CornerButtonY));
		t.Add(new PointF(BlurButtonX, BlurButtonY));
		t.Add(new PointF(BlinkSoundButtonX, BlinkSoundButtonY));
		t.Add(new PointF(SlowMoButtonX, SlowMoButtonY));
		t.Add(new PointF(PermBeltButtonX, PermBeltButtonY));
		return t;
	}

	// The stick was just used: start from the button nearest to where the cursor is.
	private void EnterSnapMode()
	{
		snapMode = true;
		snapStickDown = false;
		snapNextAt = 0;

		List<PointF> targets = GetSnapTargets();
		float best = float.MaxValue;
		snapX = 0f;
		snapY = 0f;
		for (int i = 0; i < targets.Count; i++)
		{
			float ddx = targets[i].X - cursorX;
			float ddy = targets[i].Y - cursorY;
			float d = ddx * ddx + ddy * ddy;
			if (d < best)
			{
				best = d;
				snapX = targets[i].X;
				snapY = targets[i].Y;
			}
		}
	}

	// Flick (or hold) the stick to jump to the next button in that direction, then glide the cursor onto it.
	private void UpdateSnap(float stickMag)
	{
		int nowMs = Environment.TickCount;

		if (stickMag < SnapReleaseThreshold)
		{
			snapStickDown = false;
		}
		else if (stickMag >= SnapStickThreshold)
		{
			bool jump = false;
			if (!snapStickDown)
			{
				jump = true;
				snapStickDown = true;
				snapNextAt = nowMs + SnapRepeatFirstMs;
			}
			else if (nowMs >= snapNextAt)
			{
				jump = true;
				snapNextAt = nowMs + SnapRepeatMs;
			}

			if (jump) SnapStep(padStickX / stickMag, -padStickY / stickMag);   // screen y points down
		}

		// glide to the snapped button
		float ex = snapX - cursorX;
		float ey = snapY - cursorY;
		if (SnapSpeed <= 0f || ex * ex + ey * ey < 0.25f)
		{
			cursorX = snapX;
			cursorY = snapY;
		}
		else
		{
			float dt = Math.Min(0.05f, Math.Max(0.001f, (nowMs - snapLastMs) / 1000f));
			float k = 1f - (float)Math.Exp(-SnapSpeed * dt);
			cursorX += ex * k;
			cursorY += ey * k;
		}
		snapLastMs = nowMs;
	}

	private int snapLastMs;

	// Moves the snap to the best button in the direction (dirX, dirY), a unit vector in screen space.
	private void SnapStep(float dirX, float dirY)
	{
		List<PointF> targets = GetSnapTargets();
		float bestScore = float.MaxValue;
		float bx = snapX, by = snapY;
		bool found = false;

		for (int i = 0; i < targets.Count; i++)
		{
			float vx = targets[i].X - snapX;
			float vy = targets[i].Y - snapY;
			float dist = (float)Math.Sqrt(vx * vx + vy * vy);
			if (dist < 1f) continue;   // the button we are already on

			float cos = (vx * dirX + vy * dirY) / dist;
			if (cos < SnapConeCos) continue;

			float score = dist * (1f + 3f * (1f - cos));   // near and straight ahead wins
			if (score < bestScore)
			{
				bestScore = score;
				bx = targets[i].X;
				by = targets[i].Y;
				found = true;
			}
		}

		if (found)
		{
			snapX = bx;
			snapY = by;
		}
	}

	private void UpdateCursor()
	{
		float dx = Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, 0, (int)Control.LookLeftRight);
		float dy = Function.Call<float>(Hash.GET_DISABLED_CONTROL_NORMAL, 0, (int)Control.LookUpDown);

		if (ControllerSupport && ControllerSnap)
		{
			float stickMag = (float)Math.Sqrt(padStickX * padStickX + padStickY * padStickY);
			if (stickMag > 0.25f)
			{
				if (!snapMode) EnterSnapMode();
				usingPad = true;
			}
			else if (snapMode && (Math.Abs(dx) > 0.01f || Math.Abs(dy) > 0.01f))
			{
				// the stick is at rest but the look input still moves: that is the mouse, so give it back the free cursor
				snapMode = false;
			}
			if (snapMode) UpdateSnap(stickMag);
		}
		else
		{
			snapMode = false;
		}

		if (!snapMode)
		{
			cursorX += dx * Sensitivity * 0.1f;
			cursorY += dy * Sensitivity * 0.1f;
		}

		// keep the cursor on screen (rectangular limit, so the corner button is reachable)
		cursorX = Math.Max(-620f, Math.Min(620f, cursorX));
		cursorY = Math.Max(-340f, Math.Min(340f, cursorY));

		float px = snapMode ? snapX : cursorX;
		float py = snapMode ? snapY : cursorY;

		float len = (float)Math.Sqrt(px * px + py * py);

		hovered = -1;
		boxHovered = false;
		alarmHovered = false;
		cornerHovered = false;
		blurHovered = false;
		blinkSoundHovered = false;
		slowMoHovered = false;
		permBeltHovered = false;

		// the "permanent seatbelt" button, above the slow motion button
		float permBeltReach = CornerButtonSize / 2f + 6f;
		if (Math.Abs(px - PermBeltButtonX) <= permBeltReach && Math.Abs(py - PermBeltButtonY) <= permBeltReach)
		{
			permBeltHovered = true;
			return;
		}

		// the "slow motion" button, above the blinker sounds button
		float slowMoReach = CornerButtonSize / 2f + 6f;
		if (Math.Abs(px - SlowMoButtonX) <= slowMoReach && Math.Abs(py - SlowMoButtonY) <= slowMoReach)
		{
			slowMoHovered = true;
			return;
		}

		// the "blinker sounds" button, above the blur button
		float soundReach = CornerButtonSize / 2f + 6f;
		if (Math.Abs(px - BlinkSoundButtonX) <= soundReach && Math.Abs(py - BlinkSoundButtonY) <= soundReach)
		{
			blinkSoundHovered = true;
			return;
		}

		// the small "menu blur" button in the bottom-right corner
		float blurReach = CornerButtonSize / 2f + 6f;
		if (Math.Abs(px - BlurButtonX) <= blurReach && Math.Abs(py - BlurButtonY) <= blurReach)
		{
			blurHovered = true;
			return;
		}

		// the small "change menu key" button in the top-right corner
		float cornerReach = CornerButtonSize / 2f + 6f;
		if (Math.Abs(px - CornerButtonX) <= cornerReach && Math.Abs(py - CornerButtonY) <= cornerReach)
		{
			cornerHovered = true;
			return;
		}

		// the square "keep engine running" box
		float boxDx = px - KeepEngineBoxX;
		float boxDy = py - KeepEngineBoxY;
		float reach = KeepEngineBoxSize / 2f + 6f;
		if (Math.Abs(boxDx) <= reach && Math.Abs(boxDy) <= reach)
		{
			boxHovered = true;
			return;
		}

		// the square "car alarm" box on the left
		float alarmDx = px - AlarmBoxX;
		float alarmDy = py - AlarmBoxY;
		float alarmReach = AlarmBoxSize / 2f + 6f;
		if (Math.Abs(alarmDx) <= alarmReach && Math.Abs(alarmDy) <= alarmReach)
		{
			alarmHovered = true;
			return;
		}

		if (len < DeadZone) return;

		// Pick the nearest button center to the cursor
		float best = HoverDistance;
		for (int i = 0; i < entries.Count; i++)
		{
			float ddx = px - entries[i].X;
			float ddy = py - entries[i].Y;
			float d = (float)Math.Sqrt(ddx * ddx + ddy * ddy);
			if (d < best)
			{
				best = d;
				hovered = i;
			}
		}
	}

	// ------------------------------------------------------------------
	// Sounds
	// ------------------------------------------------------------------
	private static void PlayUiSound(string name)
	{
		if (!UiSounds) return;

		try
		{
			Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, name, SoundSet, true);
		}
		catch (Exception)
		{
			// sound unavailable: ignore
		}
	}

	// Plays the hover tick when the cursor moves onto a different button.
	private void UpdateHoverSound()
	{
		int now = cornerHovered ? -3 : (blurHovered ? -5 : (blinkSoundHovered ? -6 : (slowMoHovered ? -7 : (permBeltHovered ? -8 : (boxHovered ? -2 : (alarmHovered ? -4 : hovered))))));
		if (now == lastHover) return;

		lastHover = now;
		if (now != -1) PlayUiSound(HoverSound);
	}

	// Activates a button from the menu, with the select sound (or the error sound if it is N/A).
	private void MenuToggle(Vehicle veh, Entry e)
	{
		bool available, active;
		string text;
		GetState(veh, e, out available, out active, out text);

		// the normal seatbelt button is locked while "permanent seatbelt" is forcing a state
		bool locked = e.Kind == Kind.Seatbelt && permBeltMode != 0 && !IsBikeVehicle(veh);

		PlayUiSound(available && !locked ? SelectSound : UnavailableSound);
		Toggle(veh, e);
	}

	private void MenuToggleKeepEngine(Vehicle veh)
	{
		PlayUiSound(SelectSound);
		ToggleKeepEngine(veh);
	}

	private void MenuToggleBlur()
	{
		PlayUiSound(SelectSound);
		blurEnabled = !blurEnabled;
		SaveBindings();   // remembered between sessions
	}

	private void MenuToggleBlinkSound()
	{
		PlayUiSound(SelectSound);
		blinkSoundEnabled = !blinkSoundEnabled;
		SaveBindings();   // remembered between sessions
	}

	private void MenuToggleSlowMo()
	{
		PlayUiSound(SelectSound);
		slowMoEnabled = !slowMoEnabled;
		if (menuOpen) Game.TimeScale = (slowMoEnabled && SlowMoScale < 1f) ? SlowMoScale : 1f;   // takes effect right away
		SaveBindings();   // remembered between sessions
	}

	// auto -> always fastened -> always off -> auto
	private void MenuCyclePermBelt()
	{
		PlayUiSound(SelectSound);
		permBeltMode = (permBeltMode + 1) % 3;
		SaveBindings();   // remembered between sessions
	}

	private void MenuToggleAlarm(Vehicle veh)
	{
		PlayUiSound(SelectSound);
		ToggleAlarm(veh);
	}

	// ------------------------------------------------------------------
	// Keybinds
	// ------------------------------------------------------------------
	[DllImport("user32.dll")]
	private static extern short GetAsyncKeyState(int virtualKey);

	// Ctrl / Shift / Alt as they are physically held right now.
	private static Keys HeldModifiers()
	{
		Keys mods = Keys.None;
		if ((GetAsyncKeyState(0x11) & 0x8000) != 0) mods |= Keys.Control;   // VK_CONTROL
		if ((GetAsyncKeyState(0x10) & 0x8000) != 0) mods |= Keys.Shift;     // VK_SHIFT
		if ((GetAsyncKeyState(0x12) & 0x8000) != 0) mods |= Keys.Alt;       // VK_MENU
		return mods;
	}

	// With NumLock on, Windows turns Shift + Numpad key into the matching navigation key
	// (Shift+Numpad4 arrives as the Left arrow, without Shift) and may send a fake "Shift released".
	// So the script tracks Shift itself from the key events instead of trusting the key data.
	private bool shiftTracked;                              // a Shift key has been pressed and not released
	private DateTime shiftUpUtc = DateTime.MinValue;        // when a Shift key was last released

	private static bool IsShiftKey(Keys k)
	{
		return k == Keys.ShiftKey || k == Keys.LShiftKey || k == Keys.RShiftKey;
	}

	private static bool IsNumpadKey(Keys k)
	{
		return (k >= Keys.NumPad0 && k <= Keys.NumPad9) || k == Keys.Decimal;
	}

	private static bool NavToNumpad(Keys key, out Keys numpad)
	{
		switch (key)
		{
			case Keys.Insert: numpad = Keys.NumPad0; return true;
			case Keys.End: numpad = Keys.NumPad1; return true;
			case Keys.Down: numpad = Keys.NumPad2; return true;
			case Keys.Next: numpad = Keys.NumPad3; return true;      // Page Down
			case Keys.Left: numpad = Keys.NumPad4; return true;
			case Keys.Clear: numpad = Keys.NumPad5; return true;
			case Keys.Right: numpad = Keys.NumPad6; return true;
			case Keys.Home: numpad = Keys.NumPad7; return true;
			case Keys.Up: numpad = Keys.NumPad8; return true;
			case Keys.Prior: numpad = Keys.NumPad9; return true;     // Page Up
			case Keys.Delete: numpad = Keys.Decimal; return true;
		}
		numpad = Keys.None;
		return false;
	}

	// The key plus the modifiers held. Undoes the Windows Shift+Numpad quirk described above.
	private Keys BuildKeyData(KeyEventArgs e)
	{
		Keys key = e.KeyCode;
		Keys mods = (e.KeyData & Keys.Modifiers) | HeldModifiers();

		bool shiftInKeyData = (mods & Keys.Shift) != 0;
		bool shiftJustReleased = (DateTime.UtcNow - shiftUpUtc).TotalMilliseconds < 60;

		// Shift is physically down (tracked from its key events) but the key data says it isn't,
		// or Shift was released right before this key: Windows' fake release, i.e. a numpad key.
		bool fakedRelease = shiftJustReleased || (shiftTracked && !shiftInKeyData);

		Keys numpad;
		if (NavToNumpad(key, out numpad))
		{
			Debug("Nav key " + key + ": shift in key data=" + shiftInKeyData +
				  ", shift tracked=" + shiftTracked + ", just released=" + shiftJustReleased +
				  " -> " + (fakedRelease ? "numpad key" : "arrow key"));

			if (fakedRelease)
			{
				key = numpad;
				mods |= Keys.Shift;
			}
		}
		else if (IsNumpadKey(key) && fakedRelease)
		{
			mods |= Keys.Shift;   // NumLock off: Shift+Numpad4 arrives as NumPad4 without Shift
		}
		else if (shiftTracked)
		{
			mods |= Keys.Shift;   // Shift is down but this event didn't say so
		}

		return key | mods;
	}

	private static string BindingsPath()
	{
		return Path.Combine(Directory.GetCurrentDirectory(), "scripts", KeybindFileName);
	}

	private void LoadBindings()
	{
		try
		{
			string path = BindingsPath();
			if (!File.Exists(path)) return;

			foreach (string line in File.ReadAllLines(path))
			{
				string text = line.Trim();
				if (text.Length == 0 || text.StartsWith("#") || text.StartsWith(";")) continue;

				int eq = text.IndexOf('=');
				if (eq <= 0) continue;

				string id = text.Substring(0, eq).Trim();
				string name = text.Substring(eq + 1).Trim();

				if (id == "MenuPad")
				{
					int m;
					if (TryParsePadMask(name, out m)) padOpenMask = m;
					continue;
				}

				if (id.StartsWith("Pad:"))
				{
					int m;
					if (TryParsePadMask(name, out m)) padBindings[id.Substring(4)] = m;
					continue;
				}

				if (id == "PermSeatbelt")
				{
					string m = name.ToLowerInvariant();
					permBeltMode = (m == "fastened" || m == "on" || m == "1") ? 1
								 : (m == "off" || m == "2") ? 2
								 : 0;
					continue;
				}

				if (id == "MenuBlur" || id == "BlinkerSound" || id == "MenuSlowMo")
				{
					string v = name.ToLowerInvariant();
					bool on = !(v == "off" || v == "false" || v == "0" || v == "no");
					if (id == "MenuBlur") blurEnabled = on;
					else if (id == "MenuSlowMo") slowMoEnabled = on;
					else blinkSoundEnabled = on;
					continue;
				}

				Keys k;
				if (!TryParseKey(name, out k)) continue;   // unknown key names are skipped

				if (id == "MenuKey") OpenKey = k & Keys.KeyCode;
				else bindings[id] = k;
			}
		}
		catch (Exception)
		{
			// unreadable file: start without keybinds
		}
	}

	private void SaveBindings()
	{
		try
		{
			List<string> lines = new List<string>();
			lines.Add("# Radial menu keybinds: button name = key. Combos look like Shift+F5, Ctrl+Alt+K.");
			lines.Add("# Edit here or right-click a button in the menu.");
			lines.Add("MenuKey=" + (OpenKey & Keys.KeyCode));   // the key held to open the menu
			lines.Add("MenuBlur=" + (blurEnabled ? "On" : "Off"));   // blur behind the menu (bottom-right button)
			lines.Add("BlinkerSound=" + (blinkSoundEnabled ? "On" : "Off"));   // turn-signal clicks
			lines.Add("MenuSlowMo=" + (slowMoEnabled ? "On" : "Off"));   // slow motion while the menu is open
			lines.Add("PermSeatbelt=" + (permBeltMode == 1 ? "Fastened" : permBeltMode == 2 ? "Off" : "Auto"));   // Auto / Fastened / Off in every car
			lines.Add("MenuPad=" + PadMaskToConfig(padOpenMask));   // controller buttons held to open the menu
			foreach (KeyValuePair<string, Keys> kv in bindings)
			{
				lines.Add(kv.Key + "=" + KeyToConfig(kv.Value));
			}
			foreach (KeyValuePair<string, int> kv in padBindings)
			{
				lines.Add("Pad:" + kv.Key + "=" + PadMaskToConfig(kv.Value));   // controller combo for a button
			}
			File.WriteAllLines(BindingsPath(), lines.ToArray());
		}
		catch (Exception ex)
		{
			Screen.ShowSubtitle("[WebWheelV] Could not save keybinds: " + ex.Message, 3000);
		}
	}

	private static string ModifierPrefix(Keys data)
	{
		string prefix = "";
		if ((data & Keys.Control) != 0) prefix += "Ctrl+";
		if ((data & Keys.Shift) != 0) prefix += "Shift+";
		if ((data & Keys.Alt) != 0) prefix += "Alt+";
		return prefix;
	}

	// Key as written in the keybind file, e.g. "Shift+F5".
	private static string KeyToConfig(Keys data)
	{
		return ModifierPrefix(data) + (data & Keys.KeyCode);
	}

	// Key as shown in the menu, e.g. "Shift+5" or "Ctrl+Num1".
	private static string KeyName(Keys data)
	{
		string name = (data & Keys.KeyCode).ToString();
		if (name.Length == 2 && name[0] == 'D' && char.IsDigit(name[1])) name = name.Substring(1);
		else if (name.StartsWith("NumPad")) name = "Num" + name.Substring(6);
		return ModifierPrefix(data) + name;
	}

	// >Reads "F5", "Shift+F5", "Ctrl+Alt+K"... Returns false for anything it doesn't understand.
	private static bool TryParseKey(string text, out Keys result)
	{
		result = Keys.None;

		string[] parts = text.Split('+');
		Keys mods = Keys.None;

		for (int i = 0; i < parts.Length - 1; i++)
		{
			string m = parts[i].Trim().ToLowerInvariant();
			if (m == "ctrl" || m == "control") mods |= Keys.Control;
			else if (m == "shift") mods |= Keys.Shift;
			else if (m == "alt") mods |= Keys.Alt;
			else return false;
		}

		try
		{
			Keys k = (Keys)Enum.Parse(typeof(Keys), parts[parts.Length - 1].Trim(), true);
			if (k == Keys.None) return false;
			result = k | mods;
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private string BindingText(string id)
	{
		Keys k;
		string key = bindings.TryGetValue(id, out k) ? KeyName(k) : null;

		int m;
		string pad = padBindings.TryGetValue(id, out m) ? PadMaskName(m) : null;

		// show the one that matches what the player is using right now
		return usingPad ? (pad != null ? pad : key) : (key != null ? key : pad);
	}

	// The menu key, or the controller open button when the controller is in use.
	private string OpenKeyText()
	{
		return (usingPad && padOpenMask != 0) ? PadMaskName(padOpenMask) : KeyName(OpenKey);
	}

	private string LabelOf(string id)
	{
		foreach (Entry en in entries)
		{
			if (en.Id == id) return LabelFor(en, Game.Player.Character.CurrentVehicle);
		}
		return id;
	}

	private void StartBinding(string id)
	{
		bindingId = id;
		padCaptureArmed = false;
		padCapturePeak = 0;
		bindingNotice = null;
	}

	private void EndBinding(string message)
	{
		bindingId = null;
		bindingNotice = null;
		if (message != null && ShowKeybindFeedback) Screen.ShowSubtitle("[WebWheelV] " + message, 2500);

		// If the menu key was let go while assigning, the menu was only kept open for this
		if (!openKeyHeld && !padOpenHeld) CloseMenu();
	}

	private void HandleBindingKey(Keys data)
	{
		Keys k = data & Keys.KeyCode;   // the key itself; 'data' also holds any held Ctrl/Shift/Alt

		// modifier keys on their own can't be bound: wait for a real key
		if (k == Keys.ShiftKey || k == Keys.ControlKey || k == Keys.Menu ||
			k == Keys.LShiftKey || k == Keys.RShiftKey || k == Keys.LControlKey ||
			k == Keys.RControlKey || k == Keys.LMenu || k == Keys.RMenu) return;

		if (bindingId == MenuKeyId)
		{
			HandleMenuKeyChoice(k);
			return;
		}

		if (k == OpenKey)
		{
			// while the menu key is still held these are just its auto-repeat events
			if (!openKeyHeld) bindingNotice = "That key opens the menu";
			return;
		}

		if (k == Keys.Escape)
		{
			EndBinding(null);
			return;
		}

		string label = LabelOf(bindingId);

		if (k == Keys.Back || k == Keys.Delete)
		{
			bindings.Remove(bindingId);
			SaveBindings();
			EndBinding(label + ": key cleared");
			return;
		}

		// a key can only belong to one button
		List<string> clashes = new List<string>();
		foreach (KeyValuePair<string, Keys> kv in bindings)
		{
			if (kv.Value == data && kv.Key != bindingId) clashes.Add(kv.Key);
		}
		foreach (string c in clashes) bindings.Remove(c);

		bindings[bindingId] = data;   // key + modifiers, e.g. Shift+F5
		heldKeys.Add(k);   // so the auto-repeat of this very key press doesn't trigger it
		SaveBindings();
		EndBinding(label + " bound to " + KeyName(data));
	}

	// The player pressed a key while choosing the key that opens the menu.
	private void HandleMenuKeyChoice(Keys k)
	{
		if (k == Keys.Escape)
		{
			EndBinding(null);
			return;
		}

		if (k == OpenKey)
		{
			// while the current menu key is still held these are just its auto-repeat events;
			// pressing it again afterwards means "keep it"
			if (!openKeyHeld) EndBinding("Menu key unchanged");
			return;
		}

		// the menu key is handled before keybinds, so it can't share a key with a button
		foreach (KeyValuePair<string, Keys> kv in bindings)
		{
			if ((kv.Value & Keys.KeyCode) == k)
			{
				bindingNotice = "Already used by: " + kv.Key;
				return;
			}
		}

		OpenKey = k;
		openSuppressed = true;    // ignore the new key until it is released (its auto-repeat would reopen the menu)
		openKeyHeld = false;      // the old key no longer matters
		SaveBindings();

		// The menu closes right away; the new key shows under the top-right button next time it opens
		EndBinding("Menu key set to " + KeyName(k));
	}

	private void RunKeybind(Keys data)
	{
		if (bindings.Count == 0) return;

		string id = null;
		foreach (KeyValuePair<string, Keys> kv in bindings)
		{
			if (kv.Value == data)
			{
				id = kv.Key;
				break;
			}
		}
		if (id != null) RunBindingId(id);
	}

	private void RunPadBind(int mask)
	{
		foreach (KeyValuePair<string, int> kv in padBindings)
		{
			if (kv.Value == mask)
			{
				RunBindingId(kv.Key);
				return;
			}
		}
	}

	// Runs the button with this keybind.
	private void RunBindingId(string id)
	{
		Ped player = Game.Player.Character;
		if (player == null || !player.Exists()) return;

		Vehicle veh;
		if (player.IsInVehicle())
		{
			veh = player.CurrentVehicle;
		}
		else if (id == "Door Locks")   // on foot: only the lock button, only for your last car, only when it is close
		{
			veh = Game.Player.LastVehicle;
			if (veh == null || !veh.Exists() || veh.Position.DistanceTo(player.Position) > 30f) return;
		}
		else
		{
			return;
		}

		if (veh == null || !veh.Exists()) return;

		if (id == KeepEngineId)
		{
			ToggleKeepEngine(veh);
		}
		else if (id == AlarmId)
		{
			ToggleAlarm(veh);
		}
		else
		{
			foreach (Entry en in entries)
			{
				if (en.Id == id)
				{
					Toggle(veh, en);
					break;
				}
			}
		}

		if (ShowKeybindFeedback)
		{
			feedbackId = id;
			feedbackAt = Game.GameTime + 200;   // let the change take effect before reading the state
		}
	}

	private void ShowPendingFeedback()
	{
		if (feedbackId == null || Game.GameTime < feedbackAt) return;

		string id = feedbackId;
		feedbackId = null;

		try
		{
			Ped player = Game.Player.Character;
			if (player == null || !player.Exists() || !player.IsInVehicle()) return;

			Vehicle veh = player.CurrentVehicle;
			if (veh == null || !veh.Exists()) return;

			string message = null;
			if (id == KeepEngineId)
			{
				message = id + ": " + (keepEngineOn.Contains(veh.Handle) ? "On" : "Off");
			}
			else if (id == AlarmId)
			{
				message = id + ": " + (IsAlarmSounding(veh) ? "Sounding" : "Off");
			}
			else
			{
				foreach (Entry en in entries)
				{
					if (en.Id != id) continue;

					bool available, active;
					string text;
					GetState(veh, en, out available, out active, out text);
					message = LabelFor(en, veh) + ": " + text;
					break;
				}
			}

			if (message != null) Screen.ShowSubtitle(message, 1500);
		}
		catch (Exception)
		{
			// ignore
		}
	}

	// ------------------------------------------------------------------
	// Keep engine running after getting out
	// ------------------------------------------------------------------
	private static bool IsAlarmSounding(Vehicle veh)
	{
		try
		{
			return veh.IsAlarmSounding;
		}
		catch (Exception)
		{
			return false;
		}
	}

	// Starts the car alarm if it is silent, silences it if it is sounding.
	private void ToggleAlarm(Vehicle veh)
	{
		int h = veh.Handle;

		try
		{
			if (IsAlarmSounding(veh))
			{
				try { veh.AlarmTimeLeft = 0; } catch (Exception) { }
				Function.Call(Hash.SET_VEHICLE_ALARM, h, false);
			}
			else
			{
				// the game only sounds the alarm of a vehicle whose alarm is armed
				// (stolen cars get that automatically), so arm it first
				Function.Call(Hash.SET_VEHICLE_ALARM, h, true);
				veh.StartAlarm();
				try { veh.AlarmTimeLeft = AlarmDurationMs; } catch (Exception) { }
			}
		}
		catch (Exception)
		{
			// alarm not supported on this vehicle: ignore
		}
	}

	private void ToggleKeepEngine(Vehicle veh)
	{
		int h = veh.Handle;
		bool on = !keepEngineOn.Contains(h);

		try
		{
			Function.Call(N("SET_VEHICLE_KEEP_ENGINE_ON_WHEN_ABANDONED"), h, on);
		}
		catch (Exception)
		{
			// native missing: the watcher below still keeps the engine running
		}

		if (on) keepEngineOn.Add(h); else keepEngineOn.Remove(h);
	}

	// Backup for the game's own "keep engine on when abandoned" flag: if the engine was running when
	// the player got out and it gets switched off, switch it back on for the first few seconds.
	private void WatchKeepEngine()
	{
		try
		{
			Ped player = Game.Player.Character;
			if (player == null || !player.Exists()) return;

			if (player.IsInVehicle())
			{
				Vehicle v = player.CurrentVehicle;
				if (v != null && v.Exists())
				{
					keepVehicle = v.Handle;
					exitLatched = false;
					keepReassert = false;
					keepTimer = 0f;

					if (Function.Call<bool>(Hash.GET_IS_VEHICLE_ENGINE_RUNNING, keepVehicle))
						runningStamp = Game.GameTime;
				}
				return;
			}

			if (keepVehicle == 0) return;
			int h = keepVehicle;

			if (!Function.Call<bool>(Hash.DOES_ENTITY_EXIST, h))
			{
				keepEngineOn.Remove(h);
				keepVehicle = 0;
				return;
			}

			// First moment on foot: decide whether the engine should be kept running
			if (!exitLatched)
			{
				exitLatched = true;
				keepReassert = keepEngineOn.Contains(h) && runningStamp > 0 && (Game.GameTime - runningStamp) < 2000;
			}

			if (!keepReassert) return;

			keepTimer += Game.LastFrameTime;
			if (keepTimer > 6f)
			{
				keepReassert = false;
				return;
			}

			if (!Function.Call<bool>(Hash.GET_IS_VEHICLE_ENGINE_RUNNING, h))
				Function.Call(Hash.SET_VEHICLE_ENGINE_ON, h, true, true, true);
		}
		catch (Exception)
		{
			// ignore
		}
	}

	// Draws a square button background from square.png, with a thin green frame when hovered.
	private void DrawSquareBox(float x, float y, float size, bool hover)
	{
		if (boxBg == null) return;

		try
		{
			if (hover && boxFrame != null)
			{
				float outer = size + 4f;   // thin frame: 2 units on each side
				boxFrame.Position = new PointF(x, y);
				boxFrame.Size = new SizeF(outer, outer);
				boxFrame.Centered = true;
				boxFrame.Color = C(170, 255, 185);
				boxFrame.Draw();
			}

			boxBg.Position = new PointF(x, y);
			boxBg.Size = new SizeF(size, size);
			boxBg.Centered = true;
			boxBg.Color = hover ? Color.FromArgb(225, 105, 105, 105) : Color.FromArgb(200, 62, 62, 62);
			boxBg.Draw();
		}
		catch (Exception)
		{
			boxBg = null;
			boxFrame = null;
		}
	}

	// Draws an icon sprite centered on (x, y). Returns false if there is none (or it failed).
	private static bool DrawIconSprite(ref CustomSprite icon, float x, float y, float size, Color tint)
	{
		if (icon == null) return false;

		try
		{
			icon.Position = new PointF(x, y);
			icon.Size = new SizeF(size, size);
			icon.Centered = true;
			icon.Color = tint;
			icon.Draw();
			return true;
		}
		catch (Exception)
		{
			icon = null;
			return false;
		}
	}

	private static void DrawBoxLabel(string text, float x, float y, float scale, Color c)
	{
		new TextElement(text, new PointF(x, y), scale * FontScale, c,
			MenuFont, Alignment.Center, true, true).Draw();
	}

	// The assigned key (or nothing) under a square box.
	private void DrawBoxKey(string id, float x, float y, float size)
	{
		string key = BindingText(id);
		if (key != null) DrawBoxLabel(key, x, y + size / 2f + 1f, 0.22f, Color.FromArgb(235, 255, 235, 150));
	}

	// A small on/off square button in the bottom-right corner; its icon is dimmed when off.
	private void DrawToggleButton(ref CustomSprite icon, string fallbackText, bool enabled, float x, float y, bool hover)
	{
		float size = CornerButtonSize;
		DrawSquareBox(x, y, size, hover);

		Color tint = enabled ? Color.White : Color.FromArgb(110, 255, 255, 255);
		if (!DrawIconSprite(ref icon, x, y, size * 0.74f, tint))
			DrawBoxLabel(fallbackText, x, y - 7f, 0.26f, tint);
	}

	// Three-state button: dim = auto, green = always fastened, red = always off.
	private void DrawPermBeltButton(float x, float y, bool hover)
	{
		float size = CornerButtonSize;
		DrawSquareBox(x, y, size, hover);

		Color tint = permBeltMode == 1 ? Color.FromArgb(255, 95, 235, 125)
				   : permBeltMode == 2 ? Color.FromArgb(255, 255, 90, 90)
				   : Color.FromArgb(110, 255, 255, 255);
		if (!DrawIconSprite(ref permBeltIcon, x, y, size * 0.74f, tint))
			DrawBoxLabel("BELT", x, y - 7f, 0.22f, tint);
	}

	// Center label shown while hovering an on/off button.
	private void DrawToggleLabel(float cx, float cy, string title, bool enabled)
	{
		DrawBoxLabel(title, cx, cy - 20f, 0.42f, Color.White);
		DrawBoxLabel(enabled ? "On" : "Off", cx, cy + 2f, 0.38f,
			enabled ? Color.FromArgb(255, 110, 235, 140) : Color.FromArgb(255, 235, 235, 235));
		DrawBoxLabel("Click to toggle", cx, cy + 24f, 0.26f, Color.FromArgb(170, 200, 200, 200));
	}

	// The small square in the top-right corner that lets the player choose a new menu key.
	private void DrawCornerButton(float x, float y, bool hover)
	{
		float size = CornerButtonSize;
		DrawSquareBox(x, y, size, hover);

		if (!DrawIconSprite(ref menuKeyIcon, x, y, size * 0.74f, Color.White))
			DrawBoxLabel("KEY", x, y - 7f, 0.28f, Color.White);

		// the current menu key under the button
		DrawBoxLabel(OpenKeyText(), x, y + size / 2f + 1f, 0.22f, Color.FromArgb(235, 255, 235, 150));
	}

	private void DrawAlarmBox(Vehicle veh, float x, float y, bool hover)
	{
		float size = AlarmBoxSize;
		DrawSquareBox(x, y, size, hover);

		Color tint = IsAlarmSounding(veh) ? ActiveColor(Kind.Siren, false) : C(255, 255, 255);
		if (!DrawIconSprite(ref alarmIcon, x, y, size * 0.62f, TintIcons ? tint : Color.White))
			DrawBoxLabel("ALARM", x, y - 7f, 0.26f, tint);

		DrawBoxKey(AlarmId, x, y, size);
	}

	private void DrawKeepEngineBox(Vehicle veh, float x, float y, bool hover)
	{
		float size = KeepEngineBoxSize;
		DrawSquareBox(x, y, size, hover);

		Color tint = keepEngineOn.Contains(veh.Handle) ? ActiveColor(Kind.Engine, false) : C(255, 255, 255);
		if (!DrawIconSprite(ref keepEngineIcon, x, y, size * 0.62f, TintIcons ? tint : Color.White))
			DrawBoxLabel("KEY", x, y - 8f, 0.34f, tint);

		DrawBoxKey(KeepEngineId, x, y, size);
	}

	// ------------------------------------------------------------------
	// State
	// ------------------------------------------------------------------
	private static bool DoorExists(Vehicle veh, int doorIndex)
	{
		return Function.Call<bool>(Hash.GET_IS_DOOR_VALID, veh.Handle, doorIndex);
	}

	private static readonly string[] HeadlightNames = { "Auto", "Low beam", "High beam", "Off" };

	// 0 = off, 1 = lights only, 2 = lights + sound (matches the siren icon files).
	private int GetSirenMode(Vehicle veh)
	{
		try
		{
			int h = veh.Handle;
			if (!Function.Call<bool>(N("IS_VEHICLE_SIREN_ON"), h)) return 0;
			return mutedSirens.Contains(h) ? 1 : 2;
		}
		catch (Exception)
		{
			return 0;
		}
	}

	/// The mode the script has forced on this vehicle (0 = none, the game is in control).
	private int GetHeadlightMode(int handle)
	{
		int mode;
		return headlightMode.TryGetValue(handle, out mode) ? mode : 0;
	}

	// The mode to show: the one the script forced if any, otherwise worked out from the real
	// light state so changes made with the game's own headlight key show up in the menu.
	// 0 = Auto, 1 = Low beam, 2 = High beam, 3 = Off
	private int GetEffectiveHeadlightMode(int handle)
	{
		int forced = GetHeadlightMode(handle);
		if (forced != 0) return forced;

		try
		{
			OutputArgument lightsArg = new OutputArgument();
			OutputArgument highArg = new OutputArgument();
			Function.Call(N("GET_VEHICLE_LIGHTS_STATE"), handle, lightsArg, highArg);

			bool lightsOn = lightsArg.GetResult<int>() != 0;
			bool highOn = highArg.GetResult<int>() != 0;

			if (highOn) return 2;

			// The game has no separate "auto" flag, so only call it a manual change when it
			// clearly goes against what auto would do at this time of day.
			int hour = Function.Call<int>(N("GET_CLOCK_HOURS"));
			bool deepNight = (hour >= 22 || hour < 4);
			bool midDay = (hour >= 8 && hour < 17);

			if (!lightsOn && deepNight) return 3;   // off in the dark: switched off by hand
			if (lightsOn && midDay) return 1;       // on in daylight: switched on by hand
		}
		catch (Exception)
		{
			// can't read the real state: fall through to Auto
		}

		return 0;
	}

	// If the player uses the game's own headlight key while the script is forcing a mode,
	// hand control back to the game so the key works normally again.
	private void WatchHeadlightKey()
	{
		try
		{
			Ped player = Game.Player.Character;
			if (player == null || !player.Exists() || !player.IsInVehicle()) return;

			Vehicle veh = player.CurrentVehicle;
			if (veh == null || !veh.Exists()) return;

			int h = veh.Handle;
			int forced = GetHeadlightMode(h);
			if (forced == 0) return;

			// Control 74 = INPUT_VEH_HEADLIGHT (the default H key)
			if (!Function.Call<bool>(Hash.IS_CONTROL_JUST_PRESSED, 0, 74)) return;

			headlightMode.Remove(h);
			Function.Call(N("SET_VEHICLE_LIGHTS"), h, 0);
			if (forced == 2) Function.Call(N("SET_VEHICLE_FULLBEAM"), h, false);
		}
		catch (Exception)
		{
			// ignore: worst case the key just doesn't release the override
		}
	}

	// The game has no separate "neon kit installed" flag: a car has neon exactly when its
	// neon sides are enabled. So the script remembers which sides were enabled on each vehicle,
	// which lets it switch them off and on again, and treat cars that never had neon as N/A.
	private readonly Dictionary<int, int> neonSides = new Dictionary<int, int>();   // vehicle handle -> bitmask of sides
	private readonly HashSet<int> neonOffByUs = new HashSet<int>();                 // vehicles whose neon we switched off

	private static Hash NeonGetHash()
	{
		return N("GET_VEHICLE_NEON_ENABLED", "_IS_VEHICLE_NEON_LIGHT_ENABLED", "IS_VEHICLE_NEON_LIGHT_ENABLED");
	}

	private static Hash NeonSetHash()
	{
		return N("SET_VEHICLE_NEON_ENABLED", "_SET_VEHICLE_NEON_LIGHT_ENABLED");
	}

	// Bitmask of the neon sides currently enabled (1 = left, 2 = right, 4 = front, 8 = back).
	private static int NeonMask(Vehicle veh)
	{
		Hash neonGet = NeonGetHash();
		int mask = 0;
		for (int i = 0; i < 4; i++)
		{
			if (Function.Call<bool>(neonGet, veh.Handle, i)) mask |= (1 << i);
		}
		return mask;
	}

	// Puts back any neon this script switched off (used when scripts are reloaded).
	private void RestoreNeons()
	{
		try
		{
			Hash neonSet = NeonSetHash();
			foreach (int h in neonOffByUs)
			{
				int sides;
				if (!neonSides.TryGetValue(h, out sides)) continue;

				if (!Function.Call<bool>(Hash.DOES_ENTITY_EXIST, h)) continue;

				for (int i = 0; i < 4; i++)
				{
					Function.Call(neonSet, h, i, (sides & (1 << i)) != 0);
				}
			}
		}
		catch (Exception)
		{
			// nothing to restore
		}
	}

	// True for motorcycles (class 8) and bicycles (class 13): the seatbelt button becomes a helmet button.
	private static bool IsBikeVehicle(Vehicle veh)
	{
		try
		{
			int cls = Function.Call<int>(N("GET_VEHICLE_CLASS"), veh.Handle);
			return cls == 8 || cls == 13;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static bool IsBicycle(Vehicle veh)
	{
		try { return Function.Call<int>(N("GET_VEHICLE_CLASS"), veh.Handle) == 13; }
		catch (Exception) { return false; }
	}

	// Button name for the current vehicle ("Seatbelt" becomes "Helmet" on a bike).
	private static string LabelFor(Entry e, Vehicle veh)
	{
		return (e.Kind == Kind.Seatbelt && veh != null && IsBikeVehicle(veh)) ? "Helmet" : e.Label;
	}

	private static bool IsWearingHelmet(Vehicle veh)
	{
		int ped = Game.Player.Character.Handle;
		if (Function.Call<bool>(N("IS_PED_WEARING_HELMET"), ped)) return true;

		// helmet prop put on by this script (bicycles only)
		return BicycleHelmetDrawable >= 0 && IsBicycle(veh) &&
			   Function.Call<int>(N("GET_PED_PROP_INDEX"), ped, 0) == BicycleHelmetDrawable;
	}

	// Loads a dictionary (waits a moment). False if it doesn't exist.
	private static bool LoadAnimDict(string dict)
	{
		Function.Call(N("REQUEST_ANIM_DICT"), dict);
		for (int i = 0; i < 20 && !Function.Call<bool>(N("HAS_ANIM_DICT_LOADED"), dict); i++)
			Script.Wait(10);
		return Function.Call<bool>(N("HAS_ANIM_DICT_LOADED"), dict);
	}

	private float PlayHelmetAnim(int ped, bool on, out bool reversed)
	{
		int dir = on ? 0 : 1;
		reversed = false;

		// candidates: { dict, clip, reverse }
		List<object[]> order = new List<object[]>();
		foreach (string[] a in HelmetAnims)
		{
			string clip = on ? a[1] : a[2];
			if (clip != null) order.Add(new object[] { a[0], clip, false });
		}
		if (!on)
		{
			foreach (string[] a in HelmetAnims) order.Add(new object[] { a[0], a[1], true });
		}

		string[] last = helmetAnimUsed[dir];
		if (last != null)
		{
			int idx = order.FindIndex(c => (string)c[0] == last[0] && (string)c[1] == last[1] &&
										   (bool)c[2] == helmetAnimReverse[dir]);
			if (idx > 0) { object[] first = order[idx]; order.RemoveAt(idx); order.Insert(0, first); }
		}

		foreach (object[] o in order)
		{
			string dict = (string)o[0];
			string clip = (string)o[1];
			bool reverse = (bool)o[2];
			if (!LoadAnimDict(dict)) continue;

			float durationSec = 0f;
			try
			{
				// the native has a different name in some versions
				durationSec = Function.Call<float>(
					N("GET_ENTITY_ANIM_DURATION", "_GET_ENTITY_ANIM_DURATION", "GET_ANIM_DURATION"), dict, clip);
			}
			catch (Exception)
			{
				durationSec = 0f;   // unknown: use the default below
			}
			if (durationSec < 0.3f || durationSec > 5f) durationSec = 1.5f;

			bool sameAsLast = last != null && last[0] == dict && last[1] == clip && helmetAnimReverse[dir] == reverse;
			int[] flags = sameAsLast ? new[] { helmetAnimFlag[dir] } : HelmetAnimFlags;

			foreach (int flag in flags)
			{
				Function.Call(N("TASK_PLAY_ANIM"), ped, dict, clip,
							  8f, -8f, (int)(durationSec * 1000f), flag, 0f, false, false, false);
				Script.Wait(80);

				if (Function.Call<bool>(N("IS_ENTITY_PLAYING_ANIM"), ped, dict, clip, 3))
				{
					if (reverse)
					{
						// jump near the end and play backwards
						Function.Call(N("SET_ENTITY_ANIM_CURRENT_TIME"), ped, dict, clip, 0.97f);
						Function.Call(N("SET_ENTITY_ANIM_SPEED"), ped, dict, clip, -1f);
					}

					helmetAnimUsed[dir] = new[] { dict, clip };
					helmetAnimReverse[dir] = reverse;
					helmetAnimFlag[dir] = flag;
					reversed = reverse;
					return durationSec;
				}

				Function.Call(N("STOP_ANIM_TASK"), ped, dict, clip, -4f);
			}
		}

		helmetAnimUsed[dir] = null;
		return 0f;
	}

	// Starts the helmet animation (when stopped) and schedules the actual change in the middle of it.
	private void StartHelmetChange(Vehicle veh, bool on)
	{
		if (helmetApplyAt != 0) return;   // already changing

		int ped = Game.Player.Character.Handle;
		float speed = Function.Call<float>(Hash.GET_ENTITY_SPEED, veh.Handle);
		float durationSec = 0f;
		bool reversed = false;

		try
		{
			if (speed <= HelmetAnimMaxSpeed)
				durationSec = PlayHelmetAnim(ped, on, out reversed);
		}
		catch (Exception ex)
		{
			Debug("Helmet animation failed: " + ex.Message);
			durationSec = 0f;
		}

		if (durationSec <= 0f)
		{
			SetHelmet(veh, on);   // no animation (moving, or none would play)
			return;
		}

		// The hands reach the head this far into the clip (counted from the end when it plays backwards)
		float fraction = reversed ? 1f - HelmetApplyFraction : HelmetApplyFraction;

		helmetApplyOn = on;
		helmetApplyVeh = veh.Handle;
		helmetApplyAt = Game.GameTime + (int)(durationSec * 1000f * fraction);
	}

	// Runs every frame: does the pending helmet change once the animation reaches the head.
	private void UpdateHelmetChange()
	{
		if (helmetApplyAt == 0 || Game.GameTime < helmetApplyAt) return;
		helmetApplyAt = 0;

		try
		{
			Vehicle veh = Game.Player.Character.CurrentVehicle;
			if (veh == null || !veh.Exists()) veh = Entity.FromHandle(helmetApplyVeh) as Vehicle;
			SetHelmet(veh, helmetApplyOn);
		}
		catch (Exception ex)
		{
			Debug("Helmet change failed: " + ex.Message);
		}
	}

	private static void SetHelmet(Vehicle veh, bool on)
	{
		int ped = Game.Player.Character.Handle;
		bool prop = BicycleHelmetDrawable >= 0 && IsBicycle(veh);

		if (on)
		{
			if (prop)
			{
				Function.Call(N("SET_PED_PROP_INDEX"), ped, 0, BicycleHelmetDrawable, 0, true);
			}
			else
			{
				Function.Call(N("SET_PED_CONFIG_FLAG"), ped, 35, true);           // 35 = put on a bike helmet by itself
				Function.Call(N("SET_PED_HELMET"), ped, true);                    // allow a helmet
				Function.Call(N("GIVE_PED_HELMET"), ped, false, 4096, -1);        // put it on (can be taken off again)
			}
		}
		else
		{
			// Stop the game from putting the helmet back on by itself, then take it off
			Function.Call(N("SET_PED_CONFIG_FLAG"), ped, 35, false);
			Function.Call(N("SET_PED_HELMET"), ped, false);
			Function.Call(N("REMOVE_PED_HELMET"), ped, true);   // instantly: no helmet dropped on the ground
			if (prop) Function.Call(N("CLEAR_PED_PROP"), ped, 0);
		}
	}

	// available = this feature exists / works on this vehicle
	// active    = shown in the "on" color (open door, window down, engine running, lights on...)
	// text      = state shown in the center of the menu
	private void GetState(Vehicle veh, Entry e, out bool available, out bool active, out string text)
	{
		available = true;
		active = false;
		text = "";
		int h = veh.Handle;

		try
		{
			switch (e.Kind)
			{
				case Kind.Door:
					available = DoorExists(veh, e.Index) && !veh.Doors[(VehicleDoorIndex)e.Index].IsBroken;
					active = available && veh.Doors[(VehicleDoorIndex)e.Index].IsOpen;
					text = !available ? "N/A" : (active ? "Open" : "Closed");
					break;

				case Kind.Window:
					{
						bool trackedDown = windowsDown.Contains(h * 8 + e.Index);
						available = DoorExists(veh, e.Index) &&
									(trackedDown ||
									 Function.Call<bool>(Hash.IS_VEHICLE_WINDOW_INTACT, h, e.Index));
						active = available && trackedDown;
						text = !available ? "N/A" : (active ? "Down" : "Up");
						break;
					}

				case Kind.Engine:
					active = Function.Call<bool>(Hash.GET_IS_VEHICLE_ENGINE_RUNNING, h);
					text = active ? "On" : "Off";
					break;

				case Kind.Hazard:
					active = hazardsOn.Contains(h);
					text = active ? "On" : "Off";
					break;

				case Kind.IndicatorLeft:
					active = leftOn.Contains(h);
					text = active ? "On" : "Off";
					break;

				case Kind.IndicatorRight:
					active = rightOn.Contains(h);
					text = active ? "On" : "Off";
					break;

				case Kind.InteriorLight:
					active = interiorOn.Contains(h);
					text = active ? "On" : "Off";
					break;

				case Kind.Headlights:
					{
						int mode = GetEffectiveHeadlightMode(h);
						active = (mode == 1 || mode == 2);
						text = HeadlightNames[mode];
						break;
					}

				case Kind.DoorLocks:
					{
						int status = Function.Call<int>(N("GET_VEHICLE_DOOR_LOCK_STATUS"), h);
						active = status >= 2;
						text = active ? "Locked" : "Unlocked";
						break;
					}

				case Kind.Radio:
					active = !radioOff.Contains(h);
					text = active ? "On" : "Off";
					break;

				case Kind.Seatbelt:
					{
						if (IsBikeVehicle(veh))
						{
							// bikes and bicycles: this button is the helmet
							active = IsWearingHelmet(veh);
							text = active ? "On" : "Off";
							break;
						}

						// Config flag 32 = "can fly through windscreen". Seatbelt on = flag off.
						bool canFly = Function.Call<bool>(N("GET_PED_CONFIG_FLAG"), Game.Player.Character.Handle, 32, true);
						active = !canFly;
						text = permBeltMode == 1 ? "Always fastened"
							 : permBeltMode == 2 ? "Always off"
							 : active ? "Fastened" : "Off";
						break;
					}

				case Kind.Roof:
					{
						available = Function.Call<bool>(N("IS_VEHICLE_A_CONVERTIBLE"), h, false);
						int roof = available ? Function.Call<int>(N("GET_CONVERTIBLE_ROOF_STATE"), h) : 0;
						// Game values: 0 = raised, 1 = lowering, 2 = lowered, 3 = raising
						active = available && (roof == 1 || roof == 2);   // lowered, or on its way down
						text = !available ? "N/A"
							 : roof == 0 ? "Raised"
							 : roof == 1 ? "Lowering"
							 : roof == 2 ? "Lowered"
							 : roof == 3 ? "Raising" : "Moving";
						break;
					}

				case Kind.Neon:
					{
						int mask = NeonMask(veh);
						if (mask != 0)
						{
							neonSides[h] = mask;      // remember what this car has
							neonOffByUs.Remove(h);
						}

						int known;
						available = neonSides.TryGetValue(h, out known) && known != 0;
						active = mask != 0;
						text = !available ? "N/A" : (active ? "On" : "Off");
						break;
					}

				case Kind.Siren:
					{
						available = Function.Call<int>(N("GET_VEHICLE_CLASS"), h) == 18;   // 18 = emergency
						bool on = available && Function.Call<bool>(N("IS_VEHICLE_SIREN_ON"), h);
						active = on;
						text = !available ? "N/A" : (!on ? "Off" : (mutedSirens.Contains(h) ? "Lights only" : "On"));
						break;
					}
			}
		}
		catch (Exception)
		{
			// A native that doesn't exist in this game/SHVDN version: show the button as unavailable
			available = false;
			active = false;
			text = "N/A";
		}
	}

	// ------------------------------------------------------------------
	// Actions
	// ------------------------------------------------------------------
	private void Toggle(Vehicle veh, Entry e)
	{
		bool available, active;
		string text;
		GetState(veh, e, out available, out active, out text);

		if (!available)
		{
			Debug(e.Label + " is not available");
			return;
		}

		int h = veh.Handle;

		try
		{
			switch (e.Kind)
			{
				case Kind.Door:
					{
						VehicleDoor door = veh.Doors[(VehicleDoorIndex)e.Index];
						if (door.IsOpen) door.Close(); else door.Open();
						break;
					}

				case Kind.Window:
					{
						int key = h * 8 + e.Index;
						if (windowsDown.Contains(key))
						{
							Function.Call(Hash.ROLL_UP_WINDOW, h, e.Index);
							windowsDown.Remove(key);
						}
						else
						{
							Function.Call(Hash.ROLL_DOWN_WINDOW, h, e.Index);
							windowsDown.Add(key);
						}
						break;
					}

				case Kind.Engine:
					Function.Call(Hash.SET_VEHICLE_ENGINE_ON, h, !active, true, true);
					break;

				case Kind.Hazard:
					{
						bool on = !hazardsOn.Contains(h);
						Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, h, 0, on);
						Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, h, 1, on);
						leftOn.Remove(h);
						rightOn.Remove(h);
						if (on) hazardsOn.Add(h); else hazardsOn.Remove(h);
						break;
					}

				case Kind.IndicatorLeft:
				case Kind.IndicatorRight:
					{
						bool left = (e.Kind == Kind.IndicatorLeft);
						HashSet<int> mine = left ? leftOn : rightOn;
						HashSet<int> other = left ? rightOn : leftOn;
						bool on = !mine.Contains(h);

						// native: 1 = left, 0 = right. Turning one on turns the other (and hazards) off.
						Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, h, left ? 1 : 0, on);
						Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, h, left ? 0 : 1, false);
						other.Remove(h);
						hazardsOn.Remove(h);
						if (on) mine.Add(h); else mine.Remove(h);
						break;
					}

				case Kind.InteriorLight:
					{
						bool on = !interiorOn.Contains(h);
						Function.Call(N("SET_VEHICLE_INTERIORLIGHT"), h, on);
						if (on) interiorOn.Add(h); else interiorOn.Remove(h);
						break;
					}

				case Kind.Headlights:
					{
						// Cycle: Auto -> Low beam -> High beam -> Off -> Auto
						// (continues from whatever the lights really are, including changes made with the H key)
						int mode = (GetEffectiveHeadlightMode(h) + 1) % 4;
						if (mode == 0) headlightMode.Remove(h); else headlightMode[h] = mode;
						ApplyHeadlights(h, mode);
						break;
					}

				case Kind.DoorLocks:
					// 1 = unlocked, 2 = locked
					Function.Call(N("SET_VEHICLE_DOORS_LOCKED"), h, active ? 1 : 2);
					break;

				case Kind.Radio:
					ToggleRadio(h);
					break;

				case Kind.Seatbelt:
					{
						if (IsBikeVehicle(veh))
						{
							StartHelmetChange(veh, !active);   // active = helmet on
							break;
						}

						// locked while "permanent seatbelt" is forcing a state
						if (permBeltMode != 0) break;

						// active = belted (flag off). Toggle by flipping the flag.
						int ped = Game.Player.Character.Handle;
						Function.Call(N("SET_PED_CONFIG_FLAG"), ped, 32, active);
						break;
					}

				case Kind.Roof:
					{
						int roof = Function.Call<int>(N("GET_CONVERTIBLE_ROOF_STATE"), h);

						// While the car is moving the roof animation gets cancelled half-way,
						// so in that case switch the roof instantly instead.
						bool instantly = Function.Call<float>(Hash.GET_ENTITY_SPEED, h) > RoofInstantSpeed;

						// 0 raised -> lower, 2 lowered -> raise.
						// 1 lowering / 3 raising: a click reverses the movement.
						bool lower = (roof == 0 || roof == 3);
						if (lower)
							Function.Call(N("LOWER_CONVERTIBLE_ROOF"), h, instantly);
						else
							Function.Call(N("RAISE_CONVERTIBLE_ROOF"), h, instantly);

						// At speed the game can leave the roof stuck half-way (state 1 / 3) or glitching.
						// Watch it and re-apply the instant switch until it really arrives.
						if (instantly)
						{
							roofWatchHandle = h;
							roofWatchLower = lower;
							roofWatchTarget = lower ? 2 : 0;
							roofWatchEndTime = Game.GameTime + RoofSettleMaxMs;
							roofWatchNextTry = Game.GameTime + RoofRetryMs;
						}
						else
						{
							roofWatchTarget = -1;
						}
						break;
					}

				case Kind.Neon:
					{
						Hash neonSet = NeonSetHash();
						int sides = neonSides[h];

						if (active)
						{
							// switch every side off, but remember which ones this car had
							for (int i = 0; i < 4; i++) Function.Call(neonSet, h, i, false);
							neonOffByUs.Add(h);
						}
						else
						{
							// switch back on only the sides it had
							for (int i = 0; i < 4; i++) Function.Call(neonSet, h, i, (sides & (1 << i)) != 0);
							neonOffByUs.Remove(h);
						}
						break;
					}

				case Kind.Siren:
					{
						// Off -> On (lights + sound) -> Lights only -> Off
						if (!active)
						{
							Function.Call(N("SET_VEHICLE_HAS_MUTED_SIRENS"), h, false);
							Function.Call(N("SET_VEHICLE_SIREN"), h, true);
							mutedSirens.Remove(h);
						}
						else if (!mutedSirens.Contains(h))
						{
							Function.Call(N("SET_VEHICLE_HAS_MUTED_SIRENS"), h, true);
							mutedSirens.Add(h);
						}
						else
						{
							Function.Call(N("SET_VEHICLE_SIREN"), h, false);
							Function.Call(N("SET_VEHICLE_HAS_MUTED_SIRENS"), h, false);
							mutedSirens.Remove(h);
						}
						break;
					}
			}
		}
		catch (Exception ex)
		{
			// Always shown (not just in debug mode) so a failing action doesn't go unnoticed
			Screen.ShowSubtitle("[WebWheelV] " + e.Label + " failed: " + ex.Message, 4000);
		}
	}

	private void ApplyHeadlights(int h, int mode)
	{
		// SET_VEHICLE_LIGHTS: 0 = game default, 1 = force off, 2 = force on
		switch (mode)
		{
			case 1:   // low beam
				Function.Call(N("SET_VEHICLE_LIGHTS"), h, 2);
				Function.Call(N("SET_VEHICLE_FULLBEAM"), h, false);
				break;
			case 2:   // high beam
				Function.Call(N("SET_VEHICLE_LIGHTS"), h, 2);
				Function.Call(N("SET_VEHICLE_FULLBEAM"), h, true);
				break;
			case 3:   // off
				Function.Call(N("SET_VEHICLE_LIGHTS"), h, 1);
				Function.Call(N("SET_VEHICLE_FULLBEAM"), h, false);
				break;
			default:  // auto
				Function.Call(N("SET_VEHICLE_LIGHTS"), h, 0);
				Function.Call(N("SET_VEHICLE_FULLBEAM"), h, false);
				break;
		}
	}

	private void ToggleRadio(int h)
	{
		if (radioOff.Contains(h))
		{
			// Turn the radio back on and return to the station it was on
			Function.Call(N("SET_VEHICLE_RADIO_ENABLED"), h, true);

			string station;
			if (lastStation.TryGetValue(h, out station) && !string.IsNullOrEmpty(station) && station != "OFF")
			{
				Function.Call(N("SET_VEH_RADIO_STATION"), h, station);
			}
			radioOff.Remove(h);
		}
		else
		{
			string station = Function.Call<string>(N("GET_PLAYER_RADIO_STATION_NAME"));
			lastStation[h] = station;

			Function.Call(N("SET_VEH_RADIO_STATION"), h, "OFF");
			Function.Call(N("SET_VEHICLE_RADIO_ENABLED"), h, false);
			radioOff.Add(h);
		}
	}

	// ------------------------------------------------------------------
	// Drawing primitives (coordinates are in 1280x720 virtual screen space)
	// ------------------------------------------------------------------
	private static Color C(int r, int g, int b)
	{
		return Color.FromArgb(255, r, g, b);
	}

	// ------------------------------------------------------------------
	// Menu drawing
	// ------------------------------------------------------------------
	private void DrawMenu(Vehicle veh)
	{
		float cx = Screen.Width / 2f;
		float cy = Screen.Height / 2f;

		DrawWeb(cx, cy);

		for (int i = 0; i < entries.Count; i++)
		{
			DrawButton(veh, entries[i], cx + entries[i].X, cy + entries[i].Y, i == hovered);
		}

		// The assigned key under each button that has one
		for (int i = 0; i < entries.Count; i++)
		{
			string keyText = BindingText(entries[i].Id);
			if (keyText == null) continue;

			new TextElement(keyText, new PointF(cx + entries[i].X, cy + entries[i].Y + entries[i].Radius + 1f), 0.22f * FontScale,
				Color.FromArgb(235, 255, 235, 150), MenuFont, Alignment.Center, true, true).Draw();
		}

		DrawKeepEngineBox(veh, cx + KeepEngineBoxX, cy + KeepEngineBoxY, boxHovered);
		DrawAlarmBox(veh, cx + AlarmBoxX, cy + AlarmBoxY, alarmHovered);
		DrawCornerButton(cx + CornerButtonX, cy + CornerButtonY, cornerHovered);
		DrawToggleButton(ref blurIcon, "BLUR", blurEnabled, cx + BlurButtonX, cy + BlurButtonY, blurHovered);
		DrawToggleButton(ref blinkSoundIcon, "SND", blinkSoundEnabled, cx + BlinkSoundButtonX, cy + BlinkSoundButtonY, blinkSoundHovered);
		DrawToggleButton(ref slowMoIcon, "SLO", slowMoEnabled, cx + SlowMoButtonX, cy + SlowMoButtonY, slowMoHovered);
		DrawPermBeltButton(cx + PermBeltButtonX, cy + PermBeltButtonY, permBeltHovered);

		// Center label
		if (bindingId != null)
		{
			new TextElement(usingPad ? "Press a button for" : "Press a key for", new PointF(cx, cy - 38f), 0.30f * FontScale,
				Color.FromArgb(230, 255, 255, 255), MenuFont, Alignment.Center, true, true).Draw();

			new TextElement(LabelOf(bindingId), new PointF(cx, cy - 22f), 0.42f * FontScale,
				Color.White, MenuFont, Alignment.Center, true, true).Draw();

			if (bindingId == MenuKeyId)
			{
				new TextElement(usingPad ? "The button you hold to open this menu" : "The key you hold to open this menu", new PointF(cx, cy + 2f), 0.26f * FontScale,
					Color.FromArgb(220, 200, 200, 200), MenuFont, Alignment.Center, true, true).Draw();

				new TextElement(usingPad ? "LT: cancel" : "Right-click: cancel", new PointF(cx, cy + 16f), 0.24f * FontScale,
					Color.FromArgb(190, 200, 200, 200), MenuFont, Alignment.Center, true, true).Draw();
			}
			else
			{
				new TextElement(usingPad ? "Hold several buttons for a combo" : "Hold Shift / Ctrl / Alt for a combo", new PointF(cx, cy + 2f), 0.26f * FontScale,
					Color.FromArgb(220, 200, 200, 200), MenuFont, Alignment.Center, true, true).Draw();

				new TextElement(usingPad ? "LT: cancel    Back: clear button" : "Right-click: cancel    Delete: clear key", new PointF(cx, cy + 16f), 0.24f * FontScale,
					Color.FromArgb(190, 200, 200, 200), MenuFont, Alignment.Center, true, true).Draw();
			}

			if (bindingNotice != null)
			{
				new TextElement(bindingNotice, new PointF(cx, cy + 32f), 0.30f * FontScale,
					Color.FromArgb(255, 255, 150, 120), MenuFont, Alignment.Center, true, true).Draw();
			}
		}
		else if (permBeltHovered)
		{
			DrawBoxLabel("Permanent Seatbelt", cx, cy - 20f, 0.42f, Color.White);
			DrawBoxLabel(permBeltMode == 1 ? "Always Fastened" : permBeltMode == 2 ? "Always Off" : "Auto",
				cx, cy + 2f, 0.38f,
				permBeltMode == 1 ? Color.FromArgb(255, 110, 235, 140)
				: permBeltMode == 2 ? Color.FromArgb(255, 255, 110, 110)
				: Color.FromArgb(255, 235, 235, 235));
			DrawBoxLabel("Click to change", cx, cy + 24f, 0.26f, Color.FromArgb(170, 200, 200, 200));
		}
		else if (blurHovered)
		{
			DrawToggleLabel(cx, cy, "Menu Blur", blurEnabled);
		}
		else if (blinkSoundHovered)
		{
			DrawToggleLabel(cx, cy, "Blinker Sounds", blinkSoundEnabled);
		}
		else if (slowMoHovered)
		{
			DrawToggleLabel(cx, cy, "Slow Motion", slowMoEnabled);
		}
		else if (cornerHovered)
		{
			new TextElement("Menu Key", new PointF(cx, cy - 20f), 0.42f * FontScale,
				Color.White, MenuFont, Alignment.Center, true, true).Draw();

			new TextElement("Current: " + OpenKeyText(), new PointF(cx, cy + 2f), 0.38f * FontScale,
				Color.FromArgb(255, 235, 235, 235), MenuFont, Alignment.Center, true, true).Draw();

			new TextElement("Click to change", new PointF(cx, cy + 24f), 0.26f * FontScale,
				Color.FromArgb(170, 200, 200, 200), MenuFont, Alignment.Center, true, true).Draw();
		}
		else if (alarmHovered)
		{
			bool alarmOn = IsAlarmSounding(veh);

			new TextElement("Car Alarm", new PointF(cx, cy - 20f), 0.42f * FontScale,
				Color.White, MenuFont, Alignment.Center, true, true).Draw();

			new TextElement(alarmOn ? "Sounding" : "Off", new PointF(cx, cy + 2f), 0.38f * FontScale,
				alarmOn ? Color.FromArgb(255, 110, 235, 140) : Color.FromArgb(255, 235, 235, 235),
				MenuFont, Alignment.Center, true, true).Draw();

			DrawKeyLine(cx, cy, AlarmId);
		}
		else if (boxHovered)
		{
			bool keepOn = keepEngineOn.Contains(veh.Handle);

			new TextElement("Keep Engine Running", new PointF(cx, cy - 20f), 0.42f * FontScale,
				Color.White, MenuFont, Alignment.Center, true, true).Draw();

			new TextElement(keepOn ? "On" : "Off", new PointF(cx, cy + 2f), 0.38f * FontScale,
				keepOn ? Color.FromArgb(255, 110, 235, 140) : Color.FromArgb(255, 235, 235, 235),
				MenuFont, Alignment.Center, true, true).Draw();

			DrawKeyLine(cx, cy, KeepEngineId);
		}
		else if (hovered >= 0)
		{
			bool available, active;
			string text;
			GetState(veh, entries[hovered], out available, out active, out text);

			// shadow + outline keep the text readable without a background disc
			new TextElement(LabelFor(entries[hovered], veh), new PointF(cx, cy - 20f), 0.42f * FontScale,
				Color.White, MenuFont, Alignment.Center, true, true).Draw();

			Color stateColor = !available ? Color.FromArgb(255, 170, 170, 170)
							 : active ? Color.FromArgb(255, 110, 235, 140)
							 : Color.FromArgb(255, 235, 235, 235);

			if (available && entries[hovered].Kind == Kind.Roof && IsRoofMoving(veh))
				stateColor = Color.FromArgb(255, 255, 185, 30);

			new TextElement(text, new PointF(cx, cy + 2f), 0.38f * FontScale,
				stateColor, MenuFont, Alignment.Center, true, true).Draw();

			DrawKeyLine(cx, cy, entries[hovered].Id);
		}
		else
		{
			new TextElement("Click to toggle", new PointF(cx, cy - 16f), 0.32f * FontScale,
				Color.FromArgb(220, 255, 255, 255), MenuFont, Alignment.Center, true, true).Draw();

			new TextElement("Right-click to assign a key", new PointF(cx, cy + 2f), 0.26f * FontScale,
				Color.FromArgb(190, 200, 200, 200), MenuFont, Alignment.Center, true, true).Draw();
		}

		// Cursor
		DrawSmoothCircle(cursorOuter, cx + cursorX, cy + cursorY, 5f, Color.FromArgb(220, 0, 0, 0));
		DrawSmoothCircle(cursorInner, cx + cursorX, cy + cursorY, 3.5f, Color.White);
	}

	// Shows "Key: F5" under the center label for the hovered button, if it has a key.
	private void DrawKeyLine(float cx, float cy, string id)
	{
		string keyText = BindingText(id);
		string line = keyText != null ? "Key: " + keyText : "Right-click to assign a key";

		new TextElement(line, new PointF(cx, cy + 24f), 0.26f * FontScale,
			keyText != null ? Color.FromArgb(235, 255, 235, 150) : Color.FromArgb(170, 200, 200, 200),
			MenuFont, Alignment.Center, true, true).Draw();
	}

	// Color used for a button's icon when it is "on" (open, running, locked...).
	private static Color ActiveColor(Kind kind, bool hover)
	{
		switch (kind)
		{
			case Kind.Hazard:
			case Kind.IndicatorLeft:
			case Kind.IndicatorRight:
			case Kind.InteriorLight:
				return hover ? C(225, 115, 0) : C(255, 185, 30);   // amber
			case Kind.Neon:
				return hover ? C(0, 140, 200) : C(80, 220, 255);   // cyan
			case Kind.Siren:
				return hover ? C(200, 30, 30) : C(255, 90, 90);    // red
			default:
				return hover ? C(15, 160, 55) : C(95, 235, 125);   // green
		}
	}

	// True while the convertible roof is lowering (1) or raising (3)
	private static bool IsRoofMoving(Vehicle veh)
	{
		int roof = Function.Call<int>(N("GET_CONVERTIBLE_ROOF_STATE"), veh.Handle);
		return roof == 1 || roof == 3;
	}

	private void DrawButton(Vehicle veh, Entry e, float x, float y, bool hover)
	{
		bool available, active;
		string text;
		GetState(veh, e, out available, out active, out text);

		// Hovering only lightens the circle slightly and adds a thin green ring;
		// the icon colors stay the same.
		Color bg = hover ? Color.FromArgb(225, 105, 105, 105) : Color.FromArgb(200, 62, 62, 62);
		Color dimColor = C(105, 105, 105);

		// Unavailable (N/A) buttons: much darker circle and a dark icon
		if (!available)
		{
			bg = hover ? Color.FromArgb(235, 34, 34, 34) : Color.FromArgb(235, 18, 18, 18);
			dimColor = C(62, 62, 62);
		}

		if (hover)
		{
			// thin ring: just 2 units wider than the button
			DrawSmoothCircle(e.Ring, x, y, e.Radius + 2f, available ? C(170, 255, 185) : C(95, 95, 95));
		}

		DrawSmoothCircle(e.Bg, x, y, e.Radius, bg);

		Color part = !available ? dimColor : (active ? ActiveColor(e.Kind, false) : C(255, 255, 255));

		// Convertible roof on its way up or down: amber instead of green / white
		if (available && e.Kind == Kind.Roof && IsRoofMoving(veh))
			part = ActiveColor(Kind.Hazard, false);

		// The PNG icon (the one per state for headlights / siren); a short text label if it is missing
		bool bikeButton = e.Kind == Kind.Seatbelt && IsBikeVehicle(veh);
		int stateIndex = (e.Kind == Kind.Headlights) ? GetEffectiveHeadlightMode(veh.Handle)
					   : (e.Kind == Kind.Siren) ? GetSirenMode(veh)
					   : (e.Kind == Kind.Seatbelt) ? (bikeButton ? 1 : 0)
					   : -1;

		// no helmet.png: show the text instead of a seatbelt icon on a bike
		bool noHelmetIcon = bikeButton && (e.ModeIcons == null || e.ModeIcons[1] == null);
		if (noHelmetIcon || !TryDrawPngIcon(e, x, y, part, available, stateIndex))
		{
			new TextElement(bikeButton ? "HELM" : e.Short, new PointF(x, y - 7f), 0.30f * FontScale, part,
				MenuFont, Alignment.Center, false, false).Draw();
		}
	}
}
