using Microsoft.AspNetCore.Mvc;
using TBot.WebUI.Models;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using System.Dynamic;
using Tbot.Common.Settings;
using Newtonsoft.Json.Linq;
using System.Text;

namespace TBot.WebUI.Controllers {
	public class SettingsController : Controller {
		private string GetCurrentDirectory() {
			return AppDomain.CurrentDomain.BaseDirectory;
		}
		public static bool DoesPropertyExist(dynamic settings, string name) {
			if (settings is ExpandoObject)
				return ((IDictionary<string, object>) settings).ContainsKey(name);

			return settings.GetType().GetProperty(name) != null;
		}
		private async Task<List<string>> GetFileNames() {
			var fileNames = new List<string>() { new FileInfo(SettingsService.GlobalSettingsPath).Name };
			var settingsFile = await SettingsService.GetSettings(SettingsService.GlobalSettingsPath);

			if (!SettingsService.IsSettingSet(settingsFile, "Instances")) {
				return fileNames;
			}

			foreach (var instance in settingsFile.Instances) {
				string instanceSettingsRelPath = (string) instance.Settings;
				fileNames.Add(instanceSettingsRelPath);
				fileNames.AddRange(GetProfileFileNames(instanceSettingsRelPath));
			}

			return fileNames;
		}

		/// <summary>
		/// Lists every *.json file found in the "profiles" folder next to a given instance's
		/// settings file (relative to the app's base directory, same as instanceSettingsRelPath
		/// itself) so they show up in the settings editor's file list exactly like the main
		/// settings files - this covers both the sub-blocks referenced via "Load" in that
		/// instance's settings (e.g. profiles/automine.json) and any full-overlay profile files
		/// used by the existing /loadprofile Telegram command, since both kinds simply live as
		/// *.json files under the same folder.
		/// </summary>
		private IEnumerable<string> GetProfileFileNames(string instanceSettingsRelPath) {
			string instanceDirRel = Path.GetDirectoryName(instanceSettingsRelPath) ?? "";
			string profilesDirAbs = Path.Combine(GetCurrentDirectory(), instanceDirRel, "profiles");

			if (!Directory.Exists(profilesDirAbs))
				yield break;

			foreach (var absFile in Directory.GetFiles(profilesDirAbs, "*.json").OrderBy(f => f)) {
				string relFile = Path.Combine(instanceDirRel, "profiles", Path.GetFileName(absFile));
				yield return relFile.Replace('\\', '/');
			}
		}

		[HttpGet]
		public async Task<IActionResult> Index() {
			var settingsModel = new SettingsModel();
			settingsModel.SettingsFiles = await GetFileNames();
			return View(settingsModel);
		}

		[HttpGet]
		public async Task<IActionResult> GetFileContent(string fileName) {

			string fileContents = await SettingsService.GetSettingsFileContents(Path.Combine(GetCurrentDirectory(), fileName));
			return Json(new { data = fileContents });
		}

		[HttpPost]
		public async Task<IActionResult> SaveFileContent(string fileName, string content) {
			try {
				//Validate Json, if incorrect, exception is thrown and message will be sent to the user.
				string jsonFormatted = JValue.Parse(content).ToString(Formatting.Indented);
				await SettingsService.WriteSettings(Path.Combine(GetCurrentDirectory(), fileName), jsonFormatted);
				return Json(new { success = true });
			} catch (Exception ex) {
				return Json(new { success = false, error = ex.Message });
			}
		}
	}
}
