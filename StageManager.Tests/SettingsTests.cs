using System.Windows.Input;
using StageManager.Native.PInvoke;

namespace StageManager.Tests
{
	public class SettingsTests
	{
		[Fact]
		public void Missing_values_keep_their_defaults()
		{
			var s = Settings.Parse("{}");

			Assert.Equal(6, s.MaxScenes);
			Assert.Equal(44, s.FlyInEdgeWidth);
			Assert.Equal("Ctrl+Alt+Shift+P", s.PauseHotkey);
		}

		[Fact]
		public void Comments_trailing_commas_and_out_of_range_values_are_tolerated()
		{
			var s = Settings.Parse("""
				{
					// fewer scenes
					"MaxScenes": 0,
					"FlyInEdgeWidth": -1,
					"IgnoredProcesses": null,
					"AppGroups": { "chrome": "", "msedge": "Browsers", },
				}
				""");

			Assert.Equal(6, s.MaxScenes);
			Assert.Equal(44, s.FlyInEdgeWidth);
			Assert.Empty(s.IgnoredProcesses);
			Assert.Equal("chrome", s.GetGroupKey("chrome"));
			Assert.Equal("Browsers", s.GetGroupKey("MsEdge"));
		}

		[Theory]
		[InlineData("offscreen", "OffScreen")]
		[InlineData("Minimize", "Minimize")]
		[InlineData("Cloak", "Minimize")]
		[InlineData(null, "Minimize")]
		public void HideStrategy_is_normalized(string? value, string expected)
		{
			var json = value is null ? """{ "HideStrategy": null }""" : $$"""{ "HideStrategy": "{{value}}" }""";

			Assert.Equal(expected, Settings.Parse(json).HideStrategy);
		}

		[Theory]
		[InlineData("Ctrl+Alt+Shift+P", Win32.MOD_CONTROL | Win32.MOD_ALT | Win32.MOD_SHIFT, Key.P)]
		[InlineData("win + alt + s", Win32.MOD_WIN | Win32.MOD_ALT, Key.S)]
		[InlineData("Control+F9", Win32.MOD_CONTROL, Key.F9)]
		[InlineData("Ctrl+D1", Win32.MOD_CONTROL, Key.D1)]
		public void Hotkeys_parse(string text, uint modifiers, Key key)
		{
			var s = Settings.Parse($$"""{ "PauseHotkey": "{{text}}" }""");

			Assert.True(s.TryGetPauseHotkey(out var actualModifiers, out var actualKey));
			Assert.Equal(modifiers, actualModifiers);
			Assert.Equal(key, actualKey);
		}

		[Theory]
		[InlineData("")]
		[InlineData("P")]
		[InlineData("Ctrl+1")]
		[InlineData("Hyper+P")]
		[InlineData("Ctrl+NotAKey")]
		public void Bad_hotkeys_are_rejected(string text)
		{
			var s = Settings.Parse($$"""{ "PauseHotkey": "{{text}}" }""");

			Assert.False(s.TryGetPauseHotkey(out _, out _));
		}
	}
}
