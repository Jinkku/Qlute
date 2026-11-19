using System;
using System.Globalization;
using Godot;

/// <summary>
/// Mania scroll speed on osu!mania's scale: 1..40, where a note is on screen (top of the playfield to the hit
/// line) for <see cref="MaxTimeRange"/> / speed ms. Saved in the "scrollspeed" setting; F3 / F4 change it in game
/// (also while watching a replay), like osu!mania. <see cref="Changed"/> drives the HUD popup.
/// </summary>
public static class ScrollSpeed
{
	public const string SettingKey = "scrollspeed";
	public const float Min = 1, Max = 40;
	/// <summary>20 is the fall time mania always had (~570 ms from the top to the hit line).</summary>
	public const float Default = 20;
	/// <summary>osu!mania's constant: time on screen at speed 1.</summary>
	public const float MaxTimeRange = 11485;
	public const string ActionDown = "ScrollSpeedDown", ActionUp = "ScrollSpeedUp";

	/// <summary>New speed and which way it moved (-1 slower, +1 faster, 0 set directly).</summary>
	public static event Action<float, int> Changed;

	private static float? _value;

	public static float Value
	{
		get
		{
			if (_value == null)
			{
				var raw = SettingsOperator.GetSetting(SettingKey)?.ToString();
				// older configs stored a time in ms here (1346), which never did anything: start from the default
				_value = float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= Min && v <= Max ? v : Default;
			}
			return _value.Value;
		}
	}

	/// <summary>Milliseconds a note takes from the top of the playfield to the hit line.</summary>
	public static float TimeRange => MaxTimeRange / Value;

	public static void Set(float speed, int direction = 0)
	{
		speed = Mathf.Clamp(Mathf.Round(speed), Min, Max);
		_value = speed;
		SettingsOperator.SetSetting(SettingKey, (int)speed);
		Changed?.Invoke(speed, direction);
	}

	public static void Step(int direction) => Set(Value + direction, direction);

	/// <summary>F3 / F4 (rebindable actions); added at runtime if the project doesn't define them.</summary>
	public static void EnsureActions()
	{
		void Add(string action, Key key)
		{
			if (InputMap.HasAction(action)) return;
			InputMap.AddAction(action);
			InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
		}
		Add(ActionDown, Key.F3);
		Add(ActionUp, Key.F4);
	}

	/// <summary>Call every frame where scroll speed can be changed (gameplay, replays).</summary>
	public static void PollKeys()
	{
		EnsureActions();
		if (Input.IsActionJustPressed(ActionDown)) Step(-1);
		else if (Input.IsActionJustPressed(ActionUp)) Step(1);
	}
}
