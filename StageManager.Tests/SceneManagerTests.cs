namespace StageManager.Tests
{
	public class SceneManagerTests
	{
		[Fact]
		public void Existing_windows_are_grouped_by_process()
		{
			var h = new Harness();
			var edge1 = h.Windows.Existing("msedge");
			var edge2 = h.Windows.Existing("msedge");
			var code = h.Windows.Existing("Code");
			h.Start();

			Assert.Equal(2, h.Scenes.GetScenes().Count());
			Assert.Same(h.SceneOf(edge1), h.SceneOf(edge2));
			Assert.NotSame(h.SceneOf(edge1), h.SceneOf(code));
		}

		[Fact]
		public void AppGroups_put_several_processes_in_one_scene()
		{
			var h = new Harness(Settings.Parse("""{ "AppGroups": { "MSEDGE": "Browsers", "chrome": "Browsers" } }"""));
			var edge = h.Windows.Existing("msedge");
			var chrome = h.Windows.Existing("chrome");
			h.Windows.Existing("Code");
			h.Start();

			Assert.Equal(2, h.Scenes.GetScenes().Count());
			Assert.Same(h.SceneOf(edge), h.SceneOf(chrome));
			Assert.Equal("Browsers", h.SceneOf(edge).Key);
		}

		[Fact]
		public void Switching_shows_the_scene_and_hides_everything_else()
		{
			var h = new Harness();
			var edge = h.Windows.Existing("msedge");
			var code1 = h.Windows.Existing("Code");
			var code2 = h.Windows.Existing("Code");
			h.Start();

			h.SwitchTo(h.SceneOf(code1));

			Assert.True(h.Strategy.IsVisible(code1));
			Assert.True(h.Strategy.IsVisible(code2));
			Assert.False(h.Strategy.IsVisible(edge));
			Assert.False(h.Desktop.IconsVisible);
			Assert.True(h.SceneOf(code1).IsSelected);
		}

		[Fact]
		public void Desktop_view_hides_all_windows_and_shows_icons()
		{
			var h = new Harness();
			var edge = h.Windows.Existing("msedge");
			var code = h.Windows.Existing("Code");
			h.Start();
			h.SwitchTo(h.SceneOf(code));

			h.Desktop.TrySetDesktopView(new IntPtr(99));
			h.Windows.FocusUntracked(new IntPtr(99));

			Assert.False(h.Strategy.IsVisible(edge));
			Assert.False(h.Strategy.IsVisible(code));
			Assert.True(h.Desktop.IconsVisible);
		}

		[Fact]
		public void Activating_a_window_switches_to_its_scene()
		{
			var h = new Harness();
			var edge = h.Windows.Existing("msedge");
			var code = h.Windows.Existing("Code");
			h.Start();
			h.SwitchTo(h.SceneOf(code));

			h.Windows.Activate(edge);

			Assert.True(h.Strategy.IsVisible(edge));
			Assert.False(h.Strategy.IsVisible(code));
		}

		[Fact]
		public void A_new_window_joins_its_apps_scene_and_is_shown()
		{
			var h = new Harness();
			var code = h.Windows.Existing("Code");
			var edge = h.Windows.Existing("msedge");
			h.Start();
			h.SwitchTo(h.SceneOf(code));

			var edge2 = h.Windows.Open("msedge");

			Assert.Same(h.SceneOf(edge), h.SceneOf(edge2));
			Assert.True(h.Strategy.IsVisible(edge));
			Assert.True(h.Strategy.IsVisible(edge2));
			Assert.False(h.Strategy.IsVisible(code));
		}

		[Fact]
		public void A_new_app_gets_a_new_scene()
		{
			var h = new Harness();
			h.Windows.Existing("Code");
			h.Start();

			var notepad = h.Windows.Open("notepad");

			Assert.Equal(2, h.Scenes.GetScenes().Count());
			Assert.Equal("notepad", h.SceneOf(notepad).Key);
		}

		[Fact]
		public void Closing_the_last_window_removes_the_scene()
		{
			var h = new Harness();
			var code = h.Windows.Existing("Code");
			var edge = h.Windows.Existing("msedge");
			h.Start();
			var removed = new List<Scene>();
			h.Scenes.SceneChanged += (_, e) => { if (e.Change == ChangeType.Removed) removed.Add(e.Scene); };
			var edgeScene = h.SceneOf(edge);

			h.Windows.Close(edge);

			Assert.Equal(new[] { edgeScene }, removed);
			Assert.Single(h.Scenes.GetScenes());
		}

		[Fact]
		public void Pause_restores_everything_and_ignores_activation_until_resumed()
		{
			var h = new Harness();
			var code = h.Windows.Existing("Code");
			var edge = h.Windows.Existing("msedge");
			h.Start();
			h.SwitchTo(h.SceneOf(code));

			h.Scenes.Pause();
			Assert.True(h.Strategy.IsVisible(edge));
			Assert.True(h.Desktop.IconsVisible);

			h.Windows.Activate(edge);
			Assert.True(h.Strategy.IsVisible(code));

			h.Scenes.Resume();
			Assert.True(h.Strategy.IsVisible(code));
			Assert.False(h.Strategy.IsVisible(edge));
		}

		[Fact]
		public void Stop_restores_the_desktop_once()
		{
			var h = new Harness();
			var code = h.Windows.Existing("Code");
			var edge = h.Windows.Existing("msedge");
			h.Start();
			h.SwitchTo(h.SceneOf(code));

			h.Scenes.Stop();
			h.Scenes.Stop();

			Assert.True(h.Windows.Stopped);
			Assert.True(h.Strategy.IsVisible(edge));
			Assert.Empty(h.Strategy.Hidden);
			Assert.True(h.Desktop.IconsVisible);
		}

		[Fact]
		public void Moving_a_window_into_the_current_scene_shows_it_and_drops_an_empty_source()
		{
			var h = new Harness();
			var code = h.Windows.Existing("Code");
			var edge = h.Windows.Existing("msedge");
			h.Start();
			h.SwitchTo(h.SceneOf(code));
			var codeScene = h.SceneOf(code);

			h.Scenes.MoveWindow(h.SceneOf(edge), edge, codeScene).GetAwaiter().GetResult();

			Assert.Same(codeScene, h.SceneOf(edge));
			Assert.True(h.Strategy.IsVisible(edge));
			Assert.Single(h.Scenes.GetScenes());
		}

		[Fact]
		public void Adding_a_scene_to_the_stage_moves_all_its_windows_and_remembers_the_set()
		{
			var h = new Harness();
			var code = h.Windows.Existing("Code");
			var edge1 = h.Windows.Existing("msedge");
			var edge2 = h.Windows.Existing("msedge");
			h.Start();
			h.SwitchTo(h.SceneOf(code));
			var stage = h.SceneOf(code);

			h.Scenes.AddToCurrent(h.SceneOf(edge1)).GetAwaiter().GetResult();

			Assert.Same(stage, h.SceneOf(edge1));
			Assert.Same(stage, h.SceneOf(edge2));
			Assert.True(h.Strategy.IsVisible(edge1));
			Assert.True(h.Strategy.IsVisible(edge2));
			Assert.Single(h.Scenes.GetScenes());
			Assert.Equal("Code", h.Groups.Get("msedge"));
		}

		[Fact]
		public void Adding_a_scene_in_the_desktop_view_just_switches_to_it()
		{
			var h = new Harness();
			var code = h.Windows.Existing("Code");
			var edge = h.Windows.Existing("msedge");
			h.Start();

			h.Scenes.AddToCurrent(h.SceneOf(edge)).GetAwaiter().GetResult();

			Assert.True(h.Strategy.IsVisible(edge));
			Assert.False(h.Strategy.IsVisible(code));
			Assert.Equal(2, h.Scenes.GetScenes().Count());
		}

		[Fact]
		public void Pulling_a_thumbnail_moves_that_window_onto_the_stage()
		{
			var h = new Harness();
			var code = h.Windows.Existing("Code");
			var edge1 = h.Windows.Existing("msedge");
			var edge2 = h.Windows.Existing("msedge");
			h.Start();
			h.SwitchTo(h.SceneOf(code));

			h.Scenes.PopWindowFrom(h.SceneOf(edge1), edge1).GetAwaiter().GetResult();

			Assert.Same(h.SceneOf(code), h.SceneOf(edge1));
			Assert.NotSame(h.SceneOf(code), h.SceneOf(edge2));
			Assert.True(h.Strategy.IsVisible(edge1));
			Assert.False(h.Strategy.IsVisible(edge2));
		}

		[Fact]
		public void A_just_hidden_scene_cannot_bounce_back_immediately()
		{
			var h = new Harness();
			var teams = h.Windows.Existing("Teams");
			var code = h.Windows.Existing("Code");
			h.Start();
			h.Scenes.ReentrancyWindow = TimeSpan.FromMinutes(1);
			h.SwitchTo(h.SceneOf(teams));
			h.SwitchTo(h.SceneOf(code));

			h.Windows.Activate(teams); // e.g. Teams re-activating its call window

			Assert.False(h.Strategy.IsVisible(teams));
			Assert.True(h.Strategy.IsVisible(code));
		}
	}
}
