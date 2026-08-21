using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace LWGUI
{
	[Serializable]
	public class LocalizedEntry
	{
		public string propertyName;
		public string[] translations;
	}

	public class ASPLocalizationData : ScriptableObject
	{
		public string[] languages = { "EN", "ZH", "JP" };

		[SerializeField] private LocalizedEntry[] nameEntries = Array.Empty<LocalizedEntry>();
		[SerializeField] private LocalizedEntry[] tooltipEntries = Array.Empty<LocalizedEntry>();

		private Dictionary<string, string[]> _nameLookup;
		private Dictionary<string, string[]> _tooltipLookup;

		private static ASPLocalizationData _cachedInstance;

		public static ASPLocalizationData Load()
		{
			if (_cachedInstance != null)
				return _cachedInstance;

			_cachedInstance = EditorGUIUtility.Load("ASPLocalizationData.asset") as ASPLocalizationData;
			return _cachedInstance;
		}

		public static void ClearCache()
		{
			if (_cachedInstance != null)
				_cachedInstance.InvalidateLookups();
			_cachedInstance = null;
		}

		private void OnEnable()
		{
			InvalidateLookups();
		}

		private void InvalidateLookups()
		{
			_nameLookup = null;
			_tooltipLookup = null;
		}

		private Dictionary<string, string[]> BuildLookup(LocalizedEntry[] entries)
		{
			var dict = new Dictionary<string, string[]>(entries.Length);
			foreach (var entry in entries)
			{
				if (!string.IsNullOrEmpty(entry.propertyName))
					dict[entry.propertyName] = entry.translations;
			}
			return dict;
		}

		public bool TryGetLocalizedName(string propertyName, int languageIndex, out string result)
		{
			_nameLookup ??= BuildLookup(nameEntries);
			return TryGetValue(_nameLookup, propertyName, languageIndex, out result);
		}

		public bool TryGetLocalizedTooltip(string propertyName, int languageIndex, out string result)
		{
			_tooltipLookup ??= BuildLookup(tooltipEntries);
			return TryGetValue(_tooltipLookup, propertyName, languageIndex, out result);
		}

		private static bool TryGetValue(Dictionary<string, string[]> lookup, string propertyName, int languageIndex, out string result)
		{
			result = string.Empty;
			if (!lookup.TryGetValue(propertyName, out var translations))
				return false;
			if (languageIndex < 0 || languageIndex >= translations.Length)
				return false;
			if (string.IsNullOrEmpty(translations[languageIndex]))
				return false;
			result = translations[languageIndex];
			return true;
		}

		// --- CSV Import ---

		private static readonly Regex CsvFieldRegex = new(@"(?:^|,)(?:""(?<val>(?:[^""]|"""")*)""|(?<val>[^,]*))", RegexOptions.Compiled);

		private static string[] ParseCsvLine(string line)
		{
			var fields = new List<string>();
			var matches = CsvFieldRegex.Matches(line);
			foreach (Match match in matches)
			{
				var g1 = match.Groups["val"];
				fields.Add(g1.Value.Replace("\"\"", "\""));
			}
			return fields.ToArray();
		}

		private static LocalizedEntry[] ParseCsv(string csvText)
		{
			var lines = csvText.Split('\n');
			var entries = new List<LocalizedEntry>();
			// Skip header row (line 0)
			for (int i = 1; i < lines.Length; i++)
			{
				var line = lines[i].Trim();
				if (string.IsNullOrEmpty(line))
					continue;
				var fields = ParseCsvLine(line);
				if (fields.Length < 2)
					continue;
				var entry = new LocalizedEntry
				{
					propertyName = fields[0],
					translations = new string[fields.Length - 1]
				};
				for (int j = 1; j < fields.Length; j++)
					entry.translations[j - 1] = fields[j];
				entries.Add(entry);
			}
			return entries.ToArray();
		}

		[MenuItem("ASP/Localization/Import CSV to ScriptableObject")]
		private static void ImportFromCSV()
		{
			var editorDefaultResourcesPath = Path.Combine(Application.dataPath, "Editor Default Resources");
			var namesCsvPath = Path.Combine(editorDefaultResourcesPath, "aspLocalized.csv");
			var tooltipCsvPath = Path.Combine(editorDefaultResourcesPath, "aspLocalizeToolTip.csv");
			var soAssetPath = Path.Combine(editorDefaultResourcesPath, "ASPLocalizationData.asset");
			var soRelativePath = "Assets/Editor Default Resources/ASPLocalizationData.asset";

			var data = AssetDatabase.LoadAssetAtPath<ASPLocalizationData>(soRelativePath);
			if (data == null)
			{
				data = CreateInstance<ASPLocalizationData>();
				AssetDatabase.CreateAsset(data, soRelativePath);
			}

			if (File.Exists(namesCsvPath))
			{
				var csv = File.ReadAllText(namesCsvPath);
				data.nameEntries = ParseCsv(csv);
			}
			else
			{
				Debug.LogWarning($"CSV not found: {namesCsvPath}");
			}

			if (File.Exists(tooltipCsvPath))
			{
				var csv = File.ReadAllText(tooltipCsvPath);
				data.tooltipEntries = ParseCsv(csv);
			}
			else
			{
				Debug.LogWarning($"CSV not found: {tooltipCsvPath}");
			}

			data.InvalidateLookups();
			EditorUtility.SetDirty(data);
			AssetDatabase.SaveAssets();
			ClearCache();
			Debug.Log($"ASP Localization: imported {data.nameEntries.Length} name entries, {data.tooltipEntries.Length} tooltip entries.");
		}
	}
}
