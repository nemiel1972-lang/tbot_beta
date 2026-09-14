using System;
using System.Collections.Generic;
using Tbot.Common.Settings;
using TBot.Ogame.Infrastructure.Enums;
using TBot.Ogame.Infrastructure.Models;

namespace Tbot.Helpers {

	public enum AutoMinePlanetRole {
		Home,
		Colony
	}

	/// <summary>
	/// Resolves which set of Brain.AutoMine.Max* levels applies to a given celestial.
	/// There are only two independent, self-contained base lists that do NOT inherit
	/// from each other:
	///   - Home: the top-level Max* fields, used only for the Brain.AutoMine.HomeWorld
	///     coordinate.
	///   - Colony: the Brain.AutoMine.Colony block, used as the default for every other
	///     planet.
	/// Any individual planet can additionally get its own override on top of the Colony
	/// defaults via Brain.AutoMine.ColonyOverrides (a list of {Galaxy,System,Position,Type,
	/// Max*...} entries, matched by exact coordinate) - only the fields an override entry
	/// actually sets are replaced, everything else keeps falling back to the Colony
	/// default. This is a plain "colony planet with its own numbers", not a separate role
	/// - e.g. a deuterium-focused planet is just one ColonyOverrides entry, nothing more.
	/// Moons are unaffected: they always keep using the single, shared maxLunarFacilities
	/// settings, since there is no per-role lunar configuration.
	/// </summary>
	public static class AutoMineRoleHelper {

		public static AutoMinePlanetRole GetRole(Celestial celestial, dynamic autoMineSettings, List<Celestial> allCelestials) {
			if (celestial.Coordinate.Type != Celestials.Planet)
				return AutoMinePlanetRole.Colony;

			if (SettingsService.IsSettingSet(autoMineSettings, "HomeWorld")) {
				try {
					dynamic hw = autoMineSettings.HomeWorld;
					Coordinate homeCoords = new(
						(int) hw.Galaxy,
						(int) hw.System,
						(int) hw.Position,
						Celestials.Planet
					);
					if (celestial.HasCoords(homeCoords))
						return AutoMinePlanetRole.Home;
				} catch { }
			}

			return AutoMinePlanetRole.Colony;
		}

		public static Buildings GetMaxBuildings(Celestial celestial, dynamic autoMineSettings, AutoMinePlanetRole role) {
			return new Buildings {
				MetalMine = ResolveIntField(celestial, autoMineSettings, role, "MaxMetalMine"),
				CrystalMine = ResolveIntField(celestial, autoMineSettings, role, "MaxCrystalMine"),
				DeuteriumSynthesizer = ResolveIntField(celestial, autoMineSettings, role, "MaxDeuteriumSynthetizer"),
				SolarPlant = ResolveIntField(celestial, autoMineSettings, role, "MaxSolarPlant"),
				FusionReactor = ResolveIntField(celestial, autoMineSettings, role, "MaxFusionReactor"),
				MetalStorage = ResolveIntField(celestial, autoMineSettings, role, "MaxMetalStorage"),
				CrystalStorage = ResolveIntField(celestial, autoMineSettings, role, "MaxCrystalStorage"),
				DeuteriumTank = ResolveIntField(celestial, autoMineSettings, role, "MaxDeuteriumTank")
			};
		}

		public static Facilities GetMaxFacilities(Celestial celestial, dynamic autoMineSettings, AutoMinePlanetRole role) {
			return new Facilities {
				RoboticsFactory = ResolveIntField(celestial, autoMineSettings, role, "MaxRoboticsFactory"),
				Shipyard = ResolveIntField(celestial, autoMineSettings, role, "MaxShipyard"),
				ResearchLab = ResolveIntField(celestial, autoMineSettings, role, "MaxResearchLab"),
				MissileSilo = ResolveIntField(celestial, autoMineSettings, role, "MaxMissileSilo"),
				NaniteFactory = ResolveIntField(celestial, autoMineSettings, role, "MaxNaniteFactory"),
				Terraformer = ResolveIntField(celestial, autoMineSettings, role, "MaxTerraformer"),
				SpaceDock = ResolveIntField(celestial, autoMineSettings, role, "MaxSpaceDock")
			};
		}

		/// <summary>
		/// Home: only its own top-level field, missing = 0 (no fallback to anything).
		/// Colony: own Colony-block field is the base (missing = 0, no fallback to Home),
		/// then an exact-coordinate entry in ColonyOverrides (if any matches this celestial)
		/// replaces it field-by-field - a field the override entry doesn't set simply keeps
		/// the Colony default.
		/// </summary>
		private static int ResolveIntField(Celestial celestial, dynamic autoMineSettings, AutoMinePlanetRole role, string propName) {
			if (role == AutoMinePlanetRole.Home)
				return SettingsService.GetSetting<int>(autoMineSettings, propName, 0);

			int value = 0;
			if (SettingsService.IsSettingSet(autoMineSettings, "Colony")) {
				dynamic colony = autoMineSettings.Colony;
				value = SettingsService.GetSetting<int>(colony, propName, 0);
			}

			if (SettingsService.IsSettingSet(autoMineSettings, "ColonyOverrides")) {
				try {
					foreach (var entry in autoMineSettings.ColonyOverrides) {
						Coordinate entryCoords = new(
							(int) entry.Galaxy,
							(int) entry.System,
							(int) entry.Position,
							Enum.Parse<Celestials>(entry.Type.ToString())
						);
						if (celestial.HasCoords(entryCoords)) {
							value = SettingsService.GetSetting<int>(entry, propName, value);
							break;
						}
					}
				} catch { }
			}

			return value;
		}
	}
}
