
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.GameContent.UI.Chat;
using Terraria.ModLoader.IO;
using Terraria.UI;
using Terraria.UI.Chat;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Localization;
using System.Reflection;
using System.Linq;

namespace WTIT
{
	public class WTIT : Mod
	{
		static string MouseText;
		static bool SecondLine;
		static ModHotKey ToggleTooltipsHotkey;
		static bool DisplayWorldTooltips = false;
		static bool DisplayItemTooltips = true;
		static bool DisplayTechnicalNames = false;
		
		static Preferences Configuration = new Preferences(Path.Combine(Main.SavePath, "Mod Configs", "WTIT.json"));

		public override void Load()
		{
			ToggleTooltipsHotkey = RegisterHotKey("Tile/NPC Mod Tooltip", "OemQuestion");
			if(!ReadConfig())
			{
				SetConfigDefaults();
				SaveConfig();
			}
			Configuration.AutoSave = true;
		}

		#region Config

		static bool ReadConfig()
		{
			if(Configuration.Load())
			{
				Configuration.Get("DisplayWorldTooltips", ref DisplayWorldTooltips);
				Configuration.Get("DisplayItemTooltips", ref DisplayItemTooltips);
				Configuration.Get("DisplayTechnicalNames", ref DisplayTechnicalNames);
				return true;
			}
			return false;
		}

		static void SetConfigDefaults()
		{
			DisplayWorldTooltips = false;
			DisplayItemTooltips = true;
			DisplayTechnicalNames = false;
		}

		static void SaveConfig()
		{
			Configuration.Put("DisplayWorldTooltips", DisplayWorldTooltips);
			Configuration.Put("DisplayItemTooltips", DisplayItemTooltips);
			Configuration.Put("DisplayTechnicalNames", DisplayTechnicalNames);
			Configuration.Save();
		}

		#endregion Config

		public static bool CheckAprilFools()
		{
			var date = DateTime.Now;
			return date.Month == 4 && date.Day <= 2;
		}

		#region In-World Tooltips

		public class WorldTooltips : ModPlayer
		{
            private readonly Dictionary<ushort, string> _tileTypeToName = new Dictionary<ushort, string>();

            public void InitializeTileTypes()
            {
                if (_tileTypeToName.Count != 0)
                {
                    return;
                }

                var tileId = new TileID();
                var tileIdType = typeof(TileID);
                foreach(var member in GetConstants(tileIdType))
                {
                    var name = member.Name;
                    var value = (ushort)member.GetValue(new TileID());
                    
                    _tileTypeToName[value] = name;
                }
            }

            private List<FieldInfo> GetConstants(Type type)
            {
                FieldInfo[] fieldInfos = type.GetFields(BindingFlags.Public |
                     BindingFlags.Static | BindingFlags.FlattenHierarchy);

                return fieldInfos.Where(fi => fi.IsLiteral && !fi.IsInitOnly).ToList();
            }

            public override void ProcessTriggers(TriggersSet triggersSet)
			{
				if(ToggleTooltipsHotkey.JustPressed)
				{
					if(DisplayWorldTooltips)
					{
						DisplayWorldTooltips = false;
						Main.NewText(Language.GetTextValue("Mods.WTIT.WorldTooltipsOff"));
					}
					else
					{
						DisplayWorldTooltips = true;
						Main.NewText(Language.GetTextValue("Mods.WTIT.WorldTooltipsOn"));
					}
					Configuration.Put("DisplayWorldTooltips", DisplayWorldTooltips);
				}
			}
			
			public override void PostUpdate()
			{
				if(Main.dedServ || !DisplayWorldTooltips)
					return;
				MouseText = String.Empty;
				SecondLine = false;
				var modLoaderMod = ModLoader.GetMod("ModLoader");
				
				var tile = Main.tile[Player.tileTargetX, Player.tileTargetY];
                
                if (tile != null)
                {
                    var modTile = TileLoader.GetTile(tile.type);
                    var name = "";
                    if (modTile != null)
                    {
                        name = modTile.Name;
                    }
                    if (name == "")
                    {
                        _tileTypeToName.TryGetValue(tile.type, out name);
                    }
                    MouseText = name;

                }
            }
        }

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
			if(index != -1)
			{
				//Thank you jopojelly! (taken from https://github.com/JavidPack/SummonersAssociation)
				layers.Insert(index, new LegacyGameInterfaceLayer("WMITF: Mouse Text", delegate
				{
					if(DisplayWorldTooltips && !String.IsNullOrEmpty(MouseText))
					{
						string coloredString = String.Format("[c/{1}:[{0}][c/{1}:]]", MouseText, Colors.RarityBlue.Hex3());
						var text = ChatManager.ParseMessage(coloredString, Color.White).ToArray();
						//float x = Main.fontMouseText.MeasureString(MouseText).X;
						float x = ChatManager.GetStringSize(Main.fontMouseText, text, Vector2.One).X;
						var pos = Main.MouseScreen + new Vector2(16f, 16f);
						if(pos.Y > (float)(Main.screenHeight - 30))
							pos.Y = (float)(Main.screenHeight - 30);
						if(pos.X > (float)(Main.screenWidth - x))
							pos.X = (float)(Main.screenWidth - x);
						if(SecondLine)
							pos.Y += Main.fontMouseText.LineSpacing;
						int hoveredSnippet;
						ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, Main.fontMouseText, text, pos, 0f, Vector2.Zero, Vector2.One, out hoveredSnippet);
					}
					return true;
				}, InterfaceScaleType.UI));
			}
		}

		#endregion

		#region Item Tooltips

		public class ItemTooltips : GlobalItem
		{
			public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
			{
				var modLoaderMod = ModLoader.GetMod("ModLoader");
				int mysteryItem = modLoaderMod.ItemType("MysteryItem");
				int aprilFoolsItem = modLoaderMod.ItemType("AprilFools");
				if(DisplayItemTooltips && item.type != mysteryItem && (item.type != aprilFoolsItem || !CheckAprilFools()))
				{
					if(item.modItem != null && !item.Name.Contains("[" + item.modItem.mod.Name + "]") && !item.Name.Contains("[" + item.modItem.mod.DisplayName + "]"))
					{
						string text = DisplayTechnicalNames ? (item.modItem.mod.Name + ":" + item.modItem.Name) : item.modItem.mod.DisplayName;
						var line = new TooltipLine(mod, mod.Name, "[" + text + "]");
						line.overrideColor = Colors.RarityBlue;
						tooltips.Add(line);
					}
				}
			}
		}

		#endregion

		#region Hamstar's Mod Helpers integration

		public static string GithubUserName { get { return "Sfonxs"; } }
		public static string GithubProjectName { get { return "WTIT"; } }

		public static string ConfigFileRelativePath { get { return "Mod Configs/WTIT.json"; } }

		public static void ReloadConfigFromFile()
		{
			ReadConfig();
		}

		public static void ResetConfigFromDefaults()
		{
			SetConfigDefaults();
			SaveConfig();
		}

		#endregion
	}
}
