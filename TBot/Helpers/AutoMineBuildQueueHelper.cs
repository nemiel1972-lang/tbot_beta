using System;
using System.Collections.Generic;
using System.Linq;
using Tbot.Common.Settings;
using TBot.Ogame.Infrastructure.Enums;
using TBot.Ogame.Infrastructure.Models;

namespace Tbot.Helpers {

	public enum AutoMineQueueMode {
		/// <summary>Ignore the build queue entirely, use the existing DOIR/priority logic.</summary>
		Auto,
		/// <summary>Only ever build from the queue. Once it is fully done, AutoMine stops for that celestial (nothing more to build).</summary>
		Queue,
		/// <summary>Work through the queue first (in order), then fall back to the existing DOIR/priority logic once it is fully done.</summary>
		QueueThenAuto
	}

	public struct AutoMineQueueStep {
		public Buildables Buildable;
		public int TargetLevel;
	}

	/// <summary>
	/// Resolves the optional Brain.AutoMine.BuildQueue configuration: a fixed, ordered list of
	/// "build this up to level X" steps per celestial, on top of the existing role-based
	/// (Home/Colony) Max-level logic from AutoMineRoleHelper.
	///
	/// Resolution order (most specific wins):
	///   1. Brain.AutoMine.BuildQueue.Celestials: exact-coordinate entry for this celestial
	///      (this is also how a single planet, e.g. a deuterium-focused one, gets its own
	///      build order - there is no separate "DeutPlanet" role anymore).
	///   2. Brain.AutoMine.BuildQueue.Home / .Colony: role-based entry (role from
	///      AutoMineRoleHelper.GetRole).
	///   3. Brain.AutoMine.BuildQueue.Mode as a bare default (empty queue) if nothing above matches.
	///   4. If the whole Brain.AutoMine.BuildQueue block is missing, behaves exactly as before
	///      this feature existed: Mode=Auto, empty queue, no config changes required.
	///
	/// A queue entry's own "Mode" is optional; if omitted it falls back to the parent block's
	/// Mode, and finally to Brain.AutoMine.BuildQueue.Mode (or Auto if that is missing too).
	/// </summary>
	public static class AutoMineBuildQueueHelper {

		public static (AutoMineQueueMode Mode, List<AutoMineQueueStep> Queue) Resolve(Celestial celestial, dynamic autoMineSettings, List<Celestial> allCelestials) {
			if (!SettingsService.IsSettingSet(autoMineSettings, "BuildQueue"))
				return (AutoMineQueueMode.Auto, new List<AutoMineQueueStep>());

			dynamic buildQueueSettings = autoMineSettings.BuildQueue;
			AutoMineQueueMode globalDefaultMode = ParseMode(buildQueueSettings, "Mode", AutoMineQueueMode.Auto);

			// 1. Exact-coordinate override.
			if (SettingsService.IsSettingSet(buildQueueSettings, "Celestials")) {
				try {
					foreach (var entry in buildQueueSettings.Celestials) {
						Coordinate entryCoords = new(
							(int) entry.Galaxy,
							(int) entry.System,
							(int) entry.Position,
							Enum.Parse<Celestials>(entry.Type.ToString())
						);
						if (celestial.HasCoords(entryCoords)) {
							AutoMineQueueMode entryMode = ParseMode(entry, "Mode", globalDefaultMode == AutoMineQueueMode.Auto ? AutoMineQueueMode.Queue : globalDefaultMode);
							return (entryMode, ParseQueue(entry));
						}
					}
				} catch { }
			}

			// 2. Role-based block (Home/Colony - DeutPlanet is not a separate role anymore,
			// it is just a normal colony; use BuildQueue.Celestials above for a per-planet
			// override such as the former "DeutPlanet" build order).
			var role = AutoMineRoleHelper.GetRole(celestial, autoMineSettings, allCelestials);
			string roleBlockName = role == AutoMinePlanetRole.Home ? "Home" : "Colony";
			if (SettingsService.IsSettingSet(buildQueueSettings, roleBlockName)) {
				dynamic roleBlock = GetProperty(buildQueueSettings, roleBlockName);
				AutoMineQueueMode roleMode = ParseMode(roleBlock, "Mode", globalDefaultMode);
				return (roleMode, ParseQueue(roleBlock));
			}

			// 3. Nothing specific configured for this celestial: bare global default, empty queue.
			return (globalDefaultMode, new List<AutoMineQueueStep>());
		}

		/// <summary>
		/// Returns the first not-yet-reached step in the queue (current level on the celestial
		/// still below the step's target level), or null if every step is already satisfied
		/// (queue fully done - e.g. because it was built manually, or a previous run already
		/// finished it).
		/// </summary>
		public static AutoMineQueueStep? GetNextStep(Celestial celestial, List<AutoMineQueueStep> queue) {
			foreach (var step in queue) {
				if (celestial.GetLevel(step.Buildable) < step.TargetLevel)
					return step;
			}
			return null;
		}

		private static List<AutoMineQueueStep> ParseQueue(dynamic block) {
			List<AutoMineQueueStep> output = new();
			if (!SettingsService.IsSettingSet(block, "Queue"))
				return output;
			try {
				foreach (var entry in block.Queue) {
					output.Add(new AutoMineQueueStep {
						Buildable = Enum.Parse<Buildables>(entry.Buildable.ToString()),
						TargetLevel = (int) entry.Level
					});
				}
			} catch { }
			return output;
		}

		private static AutoMineQueueMode ParseMode(dynamic block, string propName, AutoMineQueueMode fallback) {
			if (!SettingsService.IsSettingSet(block, propName))
				return fallback;
			try {
				return Enum.Parse<AutoMineQueueMode>(GetProperty(block, propName).ToString(), true);
			} catch {
				return fallback;
			}
		}

		private static dynamic GetProperty(dynamic block, string propName) {
			switch (propName) {
				case "Mode": return block.Mode;
				case "Home": return block.Home;
				case "Colony": return block.Colony;
				default: return null;
			}
		}
	}
}
