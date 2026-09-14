using System;
using System.Collections.Generic;
using Tbot.Common.Settings;
using TBot.Ogame.Infrastructure.Enums;
using TBot.Ogame.Infrastructure.Models;

namespace Tbot.Helpers {

	public enum AutoMinePlanetRole {
		Home,
		Colony,
		DeutPlanet
	}

	/// <summary>
	/// Resolves which set of Brain.AutoMine.Max* levels applies to a given celestial
	/// (top-level = Home, Colony block, or DeutPlanet block), based on the
	/// Brain.AutoMine.HomeWorld coordinate and the Brain.AutoMine.DeutPlanets list.
	/// Moons are unaffected: they always keep using the single, shared
	/// maxLunarFacilities settings, since there is no per-role lunar configuration.
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

			if (SettingsService.IsSettingSet(autoMineSettings, "DeutPlanets")) {
				try {
					foreach (var dp in autoMineSettings.DeutPlanets) {
						Coordinate deutCoords = new(
							(int) dp.Galaxy,
							(int) dp.System,
							(int) dp.Position,
							Enum.Parse<Celestials>(dp.Type.ToString())
						);
						if (celestial.HasCoords(deutCoords))
							return AutoMinePlanetRole.DeutPlanet;
					}
				} catch { }
			}

			return AutoMinePlanetRole.Colony;
		}

		public static Buildings GetMaxBuildings(dynamic autoMineSettings, AutoMinePlanetRole role) {
			return new Buildings {
				MetalMine = ResolveIntField(autoMineSettings, role, "MaxMetalMine"),
				CrystalMine = ResolveIntField(autoMineSettings, role, "MaxCrystalMine"),
				DeuteriumSynthesizer = ResolveIntField(autoMineSettings, role, "MaxDeuteriumSynthetizer"),
				SolarPlant = ResolveIntField(autoMineSettings, role, "MaxSolarPlant"),
				FusionReactor = ResolveIntField(autoMineSettings, role, "MaxFusionReactor"),
				MetalStorage = ResolveIntField(autoMineSettings, role, "MaxMetalStorage"),
				CrystalStorage = ResolveIntField(autoMineSettings, role, "MaxCrystalStorage"),
				DeuteriumTank = ResolveIntField(autoMineSettings, role, "MaxDeuteriumTank")
			};
		}

		public static Facilities GetMaxFacilities(dynamic autoMineSettings, AutoMinePlanetRole role) {
			return new Facilities {
				RoboticsFactory = ResolveIntField(autoMineSettings, role, "MaxRoboticsFactory"),
				Shipyard = ResolveIntField(autoMineSettings, role, "MaxShipyard"),
				ResearchLab = ResolveIntField(autoMineSettings, role, "MaxResearchLab"),
				MissileSilo = ResolveIntField(autoMineSettings, role, "MaxMissileSilo"),
				NaniteFactory = ResolveIntField(autoMineSettings, role, "MaxNaniteFactory"),
				Terraformer = ResolveIntField(autoMineSettings, role, "MaxTerraformer"),
				SpaceDock = ResolveIntField(autoMineSettings, role, "MaxSpaceDock")
			};
		}

		/// <summary>
		/// There are only two independent, self-contained base lists: Home (top-level
		/// Max*) and Colony. They do NOT inherit from each other - Home never falls
		/// back to Colony and Colony never falls back to Home; a field missing in
		/// either one's own block simply resolves to 0.
		/// DeutPlanet is not a third independent list: it is treated as a normal
		/// Colony (Colony is its base), with its own configured fields overriding
		/// Colony's field-by-field on top.
		/// </summary>
		private static int ResolveIntField(dynamic autoMineSettings, AutoMinePlanetRole role, string propName) {
			switch (role) {
				case AutoMinePlanetRole.Home:
					return SettingsService.GetSetting<int>(autoMineSettings, propName, 0);

				case AutoMinePlanetRole.Colony: {
					if (!SettingsService.IsSettingSet(autoMineSettings, "Colony"))
						return 0;
					dynamic colony = autoMineSettings.Colony;
					return SettingsService.GetSetting<int>(colony, propName, 0);
				}

				case AutoMinePlanetRole.DeutPlanet: {
					int baseValue = 0;
					if (SettingsService.IsSettingSet(autoMineSettings, "Colony")) {
						dynamic colony = autoMineSettings.Colony;
						baseValue = SettingsService.GetSetting<int>(colony, propName, 0);
					}
					if (SettingsService.IsSettingSet(autoMineSettings, "DeutPlanet")) {
						dynamic deutPlanet = autoMineSettings.DeutPlanet;
						baseValue = SettingsService.GetSetting<int>(deutPlanet, propName, baseValue);
					}
					return baseValue;
				}

				default:
					return 0;
			}
		}
	}
}
