using System.IO;

namespace StageManager.Tests
{
	public class AppSetTests
	{
		[Fact]
		public void OneWindowPerScene_gives_every_window_its_own_scene()
		{
			var h = new Harness(Settings.Parse("""{ "OneWindowPerScene": true }"""));
			var edge1 = h.Windows.Existing("msedge");
			var edge2 = h.Windows.Existing("msedge");
			h.Start();

			Assert.NotSame(h.SceneOf(edge1), h.SceneOf(edge2));
		}

		[Fact]
		public void SeparateWindowApps_only_split_the_listed_apps()
		{
			var h = new Harness(Settings.Parse("""{ "SeparateWindowApps": ["teams"] }"""));
			var teams1 = h.Windows.Existing("Teams");
			var teams2 = h.Windows.Existing("Teams");
			var edge1 = h.Windows.Existing("msedge");
			var edge2 = h.Windows.Existing("msedge");
			h.Start();

			var teams3 = h.Windows.Open("Teams");

			Assert.NotSame(h.SceneOf(teams1), h.SceneOf(teams2));
			Assert.NotSame(h.SceneOf(teams1), h.SceneOf(teams3));
			Assert.Same(h.SceneOf(edge1), h.SceneOf(edge2));
		}

		[Fact]
		public void Moving_all_of_an_apps_windows_into_a_scene_makes_its_new_windows_open_there()
		{
			var h = new Harness();
			var code = h.Windows.Existing("Code");
			var edge = h.Windows.Existing("msedge");
			h.Start();
			var codeScene = h.SceneOf(code);

			h.Move(edge, codeScene);
			var edge2 = h.Windows.Open("msedge");

			Assert.Same(codeScene, h.SceneOf(edge2));
			Assert.Equal("Code", h.Groups.Get("msedge"));
		}

		[Fact]
		public void Learned_sets_survive_a_restart()
		{
			var groups = new GroupMemory();
			var first = new Harness(groups: groups);
			var code = first.Windows.Existing("Code");
			var edge = first.Windows.Existing("msedge");
			first.Start();
			first.Move(edge, first.SceneOf(code));

			var second = new Harness(groups: groups);
			var code2 = second.Windows.Existing("Code");
			var edge2 = second.Windows.Existing("msedge");
			second.Start();

			Assert.Same(second.SceneOf(code2), second.SceneOf(edge2));
		}

		[Fact]
		public void Learned_sets_are_saved_to_disk()
		{
			var path = Path.Combine(Path.GetTempPath(), $"stagemanager-groups-{Guid.NewGuid()}.json");
			try
			{
				GroupMemory.Load(path).Remember("msedge", "Code");

				Assert.Equal("Code", GroupMemory.Load(path).Get("MSEDGE"));
			}
			finally
			{
				File.Delete(path);
			}
		}

		[Fact]
		public void Moving_only_some_of_an_apps_windows_learns_nothing()
		{
			var h = new Harness();
			var code = h.Windows.Existing("Code");
			var edge1 = h.Windows.Existing("msedge");
			h.Windows.Existing("msedge");
			h.Start();

			h.Move(edge1, h.SceneOf(code));

			Assert.Null(h.Groups.Get("msedge"));
		}

		[Fact]
		public void Moving_an_app_on_to_another_scene_relearns_its_set()
		{
			var h = new Harness();
			var code = h.Windows.Existing("Code");
			var slack = h.Windows.Existing("slack");
			var edge = h.Windows.Existing("msedge");
			h.Start();

			h.Move(edge, h.SceneOf(code));
			h.Move(edge, h.SceneOf(slack));

			Assert.Equal("slack", h.Groups.Get("msedge"));
		}

		[Fact]
		public void Moving_a_whole_app_into_a_single_window_scene_learns_nothing()
		{
			var h = new Harness(Settings.Parse("""{ "SeparateWindowApps": ["Teams"] }"""));
			var teams = h.Windows.Existing("Teams");
			var edge = h.Windows.Existing("msedge");
			h.Start();

			h.Move(edge, h.SceneOf(teams));

			Assert.Null(h.Groups.Get("msedge"));
		}

		[Fact]
		public void AppGroups_in_settings_win_over_learned_sets()
		{
			var groups = new GroupMemory();
			groups.Remember("msedge", "Code");
			var h = new Harness(Settings.Parse("""{ "AppGroups": { "msedge": "Browsers", "chrome": "Browsers" } }"""), groups);
			var edge = h.Windows.Existing("msedge");
			var chrome = h.Windows.Existing("chrome");
			var code = h.Windows.Existing("Code");
			h.Start();

			Assert.Same(h.SceneOf(edge), h.SceneOf(chrome));
			Assert.NotSame(h.SceneOf(edge), h.SceneOf(code));
		}

		[Fact]
		public void Forgetting_sets_lets_new_windows_open_in_their_own_scene()
		{
			var h = new Harness();
			var code = h.Windows.Existing("Code");
			var edge = h.Windows.Existing("msedge");
			h.Start();
			h.Move(edge, h.SceneOf(code));

			h.Scenes.ForgetLearnedGroups();
			var edge2 = h.Windows.Open("msedge");

			Assert.NotSame(h.SceneOf(code), h.SceneOf(edge2));
		}
	}
}
