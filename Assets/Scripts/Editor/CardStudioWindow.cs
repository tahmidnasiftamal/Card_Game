#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CrimsonDynasty.EditorTools
{
    public class CardStudioWindow : EditorWindow
    {
        private enum Tab { Data, Database, Prefabs, Validate, Import, Preview }
        private Tab _tab;

        // ---- Shared state ----
        private GUIStyle _header;
        private Vector2 _scroll;

        // =======================
        // DATA TAB (Create/Edit)
        // =======================
        // Target asset (optional) for editing
        private CrimsonDynasty.CardData _editCard;

        // Fields for creating/editing (mirrors CardData)
        private Sprite _portrait;
        private string _name;
        private string _title;
        private string _house;
        private string _loyalty;
        private int _age;
        private int _health = 100;
        private int _xp;
        private int _influenceRate;
        private string _specialAbility;
        private List<string> _oathSlots = new();
        private List<string> _status = new();

        private DefaultAsset _dataOutputFolder;
        private string _assetNameOverride = "";

        // =======================
        // DATABASE TAB
        // =======================
        private CrimsonDynasty.CardDatabase _database;
        private DefaultAsset _autoAddFolder;
        private string _dbFilter = "";

        // =======================
        // PREFABS TAB
        // =======================
        private GameObject _baseCardPrefab; // must have CardView
        private DefaultAsset _prefabOutputFolder;
        private DefaultAsset _cardDataFolderForPrefabs;
        private List<CrimsonDynasty.CardData> _explicitCardsForPrefabs = new();
        private CrimsonDynasty.CardDatabase _dbForPrefabs;

        // =======================
        // VALIDATE TAB
        // =======================
        private DefaultAsset _validateFolder;
        private CrimsonDynasty.CardDatabase _validateDb;
        private List<string> _validateReport = new();

        // =======================
        // IMPORT TAB (basic CSV)
        // =======================
        private TextAsset _csv;
        private DefaultAsset _importOutputFolder;
        private bool _importUpdateExisting = true;

        // =======================
        // PREVIEW TAB
        // =======================
        private CrimsonDynasty.CardDatabase _previewDb;
        private DefaultAsset _previewFolder;
        private List<CrimsonDynasty.CardData> _previewExplicit = new();

        [MenuItem("Tools/Crimson Dynasty/Card Studio")]
        public static void Open() => GetWindow<CardStudioWindow>("Card Studio");

        private void OnEnable()
        {
            _header = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
        }

        private void OnGUI()
        {
            DrawToolbar();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case Tab.Data: DrawTabData(); break;
                case Tab.Database: DrawTabDatabase(); break;
                case Tab.Prefabs: DrawTabPrefabs(); break;
                case Tab.Validate: DrawTabValidate(); break;
                case Tab.Import: DrawTabImport(); break;
                case Tab.Preview: DrawTabPreview(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.Space(4);
            _tab = (Tab)GUILayout.Toolbar((int)_tab, new[] { "Data", "Database", "Prefabs", "Validate", "Import", "Preview" });
            EditorGUILayout.Space(6);
        }

        // =======================
        // TAB: DATA
        // =======================
        private void DrawTabData()
        {
            GUILayout.Label("Create / Edit CardData", _header);

            _editCard = (CrimsonDynasty.CardData)EditorGUILayout.ObjectField("Edit Target (optional)", _editCard, typeof(CrimsonDynasty.CardData), false);

            if (_editCard && GUILayout.Button("Load From Target"))
            {
                LoadFromCard(_editCard);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Fields", EditorStyles.boldLabel);
            _portrait = (Sprite)EditorGUILayout.ObjectField("Portrait", _portrait, typeof(Sprite), false);
            _name = EditorGUILayout.TextField("Name", _name);
            _title = EditorGUILayout.TextField("Title", _title);
            _house = EditorGUILayout.TextField("House", _house);
            _loyalty = EditorGUILayout.TextField("Loyalty", _loyalty);
            _age = EditorGUILayout.IntField("Age", _age);
            _health = EditorGUILayout.IntField("Health", _health);
            _xp = EditorGUILayout.IntField("XP", _xp);
            _influenceRate = EditorGUILayout.IntField("Influence Rate", _influenceRate);
            _specialAbility = EditorGUILayout.TextField("Special Ability", _specialAbility);

            DrawStringList("Oath Slots", _oathSlots);
            DrawStringList("Status", _status);

            EditorGUILayout.Space(6);
            _dataOutputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", _dataOutputFolder, typeof(DefaultAsset), false);
            _assetNameOverride = EditorGUILayout.TextField("Asset Name Override", _assetNameOverride);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create New CardData", GUILayout.Height(26)))
                    CreateCardData();

                using (new EditorGUI.DisabledScope(_editCard == null))
                {
                    if (GUILayout.Button("Apply To Target", GUILayout.Height(26)))
                        ApplyToExisting(_editCard);
                }
            }
        }

        private void LoadFromCard(CrimsonDynasty.CardData cd)
        {
            _portrait = cd.Portrait;
            _name = cd.CharacterName;
            _title = cd.Title;
            _house = cd.House;
            _loyalty = cd.Loyalty;
            _age = cd.Age;
            _health = cd.Health;
            _xp = cd.XP;
            _influenceRate = cd.InfluenceRate;
            _specialAbility = cd.SpecialAbility;
            _oathSlots = new List<string>(cd.OathSlots ?? Array.Empty<string>());
            _status = new List<string>(cd.Status ?? Array.Empty<string>());
        }

        private void CreateCardData()
        {
            if (!ValidateFolder(_dataOutputFolder, out var folderPath)) return;

            string safeName = string.IsNullOrWhiteSpace(_assetNameOverride)
                ? MakeSafeName($"{_name}_{_house}")
                : MakeSafeName(_assetNameOverride);

            if (string.IsNullOrEmpty(safeName)) safeName = "Card_NewCharacter";
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folderPath, $"{safeName}.asset"));

            var card = ScriptableObject.CreateInstance<CrimsonDynasty.CardData>();
            WriteFieldsToCard(card);
            AssetDatabase.CreateAsset(card, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = card;
            EditorGUIUtility.PingObject(card);
            EditorUtility.DisplayDialog("Card Studio", $"Created CardData:\n{assetPath}", "OK");
        }

        private void ApplyToExisting(CrimsonDynasty.CardData card)
        {
            if (!card) return;
            WriteFieldsToCard(card);
            EditorUtility.SetDirty(card);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Card Studio", "Applied fields to selected CardData.", "OK");
        }

        private void WriteFieldsToCard(CrimsonDynasty.CardData card)
        {
            var so = new SerializedObject(card);
            so.FindProperty("portrait").objectReferenceValue = _portrait;
            so.FindProperty("characterName").stringValue = _name ?? string.Empty;
            so.FindProperty("title").stringValue = _title ?? string.Empty;
            so.FindProperty("house").stringValue = _house ?? string.Empty;
            so.FindProperty("loyalty").stringValue = _loyalty ?? string.Empty;
            so.FindProperty("age").intValue = _age;
            so.FindProperty("health").intValue = _health;
            so.FindProperty("xp").intValue = _xp;
            so.FindProperty("influenceRate").intValue = _influenceRate;
            so.FindProperty("specialAbility").stringValue = _specialAbility ?? string.Empty;

            var oathProp = so.FindProperty("oathSlots");
            oathProp.ClearArray();
            for (int i = 0; i < _oathSlots.Count; i++)
            {
                oathProp.InsertArrayElementAtIndex(i);
                oathProp.GetArrayElementAtIndex(i).stringValue = _oathSlots[i] ?? string.Empty;
            }

            var statusProp = so.FindProperty("status");
            statusProp.ClearArray();
            for (int i = 0; i < _status.Count; i++)
            {
                statusProp.InsertArrayElementAtIndex(i);
                statusProp.GetArrayElementAtIndex(i).stringValue = _status[i] ?? string.Empty;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DrawStringList(string label, List<string> list)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            int remove = -1;
            for (int i = 0; i < list.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    list[i] = EditorGUILayout.TextField(list[i]);
                    if (GUILayout.Button("X", GUILayout.Width(22)))
                        remove = i;
                }
            }
            if (remove >= 0) list.RemoveAt(remove);
            if (GUILayout.Button($"+ Add {label[..Mathf.Min(label.Length, Math.Max(0, label.IndexOf(' ')))]}"))
                list.Add(string.Empty);
        }

        // =======================
        // TAB: DATABASE
        // =======================
        private void DrawTabDatabase()
        {
            GUILayout.Label("Manage CardDatabase", _header);
            _database = (CrimsonDynasty.CardDatabase)EditorGUILayout.ObjectField("Database", _database, typeof(CrimsonDynasty.CardDatabase), false);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create New Database"))
                    CreateDatabase();
                using (new EditorGUI.DisabledScope(!_database))
                {
                    if (GUILayout.Button("Clear Database"))
                    {
                        _database.cards.Clear();
                        EditorUtility.SetDirty(_database);
                        AssetDatabase.SaveAssets();
                    }
                }
            }

            EditorGUILayout.Space(4);
            _autoAddFolder = (DefaultAsset)EditorGUILayout.ObjectField("Auto-Add From Folder", _autoAddFolder, typeof(DefaultAsset), false);
            if (GUILayout.Button("Add All CardData From Folder"))
            {
                var list = FindCardDataInFolder(_autoAddFolder);
                if (_database && list.Count > 0)
                {
                    foreach (var cd in list) if (!_database.cards.Contains(cd)) _database.cards.Add(cd);
                    EditorUtility.SetDirty(_database);
                    AssetDatabase.SaveAssets();
                }
            }

            _dbFilter = EditorGUILayout.TextField("Filter (by Name/House)", _dbFilter);

            if (_database)
            {
                EditorGUILayout.Space(6);
                GUILayout.Label($"Cards in '{_database.name}' ({_database.cards.Count})", EditorStyles.boldLabel);
                for (int i = _database.cards.Count - 1; i >= 0; i--)
                {
                    var cd = _database.cards[i];
                    if (!cd) { _database.cards.RemoveAt(i); continue; }

                    if (!string.IsNullOrEmpty(_dbFilter))
                    {
                        var match = (cd.CharacterName?.IndexOf(_dbFilter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                                    || (cd.House?.IndexOf(_dbFilter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
                        if (!match) continue;
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(cd, typeof(CrimsonDynasty.CardData), false);
                        if (GUILayout.Button("Remove", GUILayout.Width(70)))
                            _database.cards.RemoveAt(i);
                    }
                }
            }
        }

        private void CreateDatabase()
        {
            var path = EditorUtility.SaveFilePanelInProject("New CardDatabase", "CardDatabase", "asset", "Choose a location.");
            if (string.IsNullOrEmpty(path)) return;
            var db = ScriptableObject.CreateInstance<CrimsonDynasty.CardDatabase>();
            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();
            _database = db;
            EditorGUIUtility.PingObject(db);
        }

        // =======================
        // TAB: PREFABS
        // =======================
        private void DrawTabPrefabs()
        {
            GUILayout.Label("Generate Prefabs / Spawn", _header);

            _baseCardPrefab = (GameObject)EditorGUILayout.ObjectField("Base Card Prefab", _baseCardPrefab, typeof(GameObject), false);
            _prefabOutputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", _prefabOutputFolder, typeof(DefaultAsset), false);

            EditorGUILayout.Space(4);
            GUILayout.Label("Sources", EditorStyles.boldLabel);
            _dbForPrefabs = (CrimsonDynasty.CardDatabase)EditorGUILayout.ObjectField("Database (optional)", _dbForPrefabs, typeof(CrimsonDynasty.CardDatabase), false);
            _cardDataFolderForPrefabs = (DefaultAsset)EditorGUILayout.ObjectField("CardData Folder (optional)", _cardDataFolderForPrefabs, typeof(DefaultAsset), false);

            EditorGUILayout.LabelField("Explicit Cards (optional)");
            int remove = -1;
            for (int i = 0; i < _explicitCardsForPrefabs.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _explicitCardsForPrefabs[i] = (CrimsonDynasty.CardData)EditorGUILayout.ObjectField(_explicitCardsForPrefabs[i], typeof(CrimsonDynasty.CardData), false);
                    if (GUILayout.Button("X", GUILayout.Width(22))) remove = i;
                }
            }
            if (remove >= 0) _explicitCardsForPrefabs.RemoveAt(remove);
            if (GUILayout.Button("+ Add Card")) _explicitCardsForPrefabs.Add(null);

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Prefab Assets", GUILayout.Height(26)))
                    GeneratePrefabs();

                if (GUILayout.Button("Spawn Scene Instances", GUILayout.Height(26)))
                    SpawnPreviewFromSources();
            }
        }

        private void GeneratePrefabs()
        {
            if (!_baseCardPrefab) { Dialog("Assign a Base Card Prefab."); return; }
            if (!ValidateFolder(_prefabOutputFolder, out string outFolder)) return;

            var view = _baseCardPrefab.GetComponent<CrimsonDynasty.CardView>();
            if (!view) { Dialog("Base Card Prefab must have a CardView component."); return; }

            var cards = CollectFromSources(_dbForPrefabs, _cardDataFolderForPrefabs, _explicitCardsForPrefabs);
            if (cards.Count == 0) { Dialog("No CardData sources found."); return; }

            int created = 0;
            foreach (var cd in cards)
            {
                var temp = (GameObject)PrefabUtility.InstantiatePrefab(_baseCardPrefab);
                try
                {
                    temp.GetComponent<CrimsonDynasty.CardView>().Data = cd;
                    string nameCore = MakeSafeName($"{cd.CharacterName}_{cd.House}");
                    string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(outFolder, nameCore + ".prefab"));
                    PrefabUtility.SaveAsPrefabAsset(temp, path);
                    created++;
                }
                finally { DestroyImmediate(temp); }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Dialog($"Generated {created} prefab(s).");
        }

        private void SpawnPreviewFromSources()
        {
            if (!_baseCardPrefab) { Dialog("Assign a Base Card Prefab."); return; }
            var view = _baseCardPrefab.GetComponent<CrimsonDynasty.CardView>();
            if (!view) { Dialog("Base Card Prefab must have a CardView component."); return; }

            var cards = CollectFromSources(_dbForPrefabs, _cardDataFolderForPrefabs, _explicitCardsForPrefabs);
            if (cards.Count == 0) { Dialog("No CardData sources found."); return; }

            var parent = new GameObject("CardStudio_Preview");
            Undo.RegisterCreatedObjectUndo(parent, "Spawn Cards");

            float x = 0f;
            foreach (var cd in cards)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(_baseCardPrefab);
                inst.name = $"{cd.CharacterName}_{cd.House}";
                inst.GetComponent<CrimsonDynasty.CardView>().Data = cd;
                inst.transform.SetParent(parent.transform, false);
                inst.transform.localPosition = new Vector3(x, 0, 0);
                x += 1.5f;
            }
            Selection.activeObject = parent;
        }

        // =======================
        // TAB: VALIDATE
        // =======================
        private void DrawTabValidate()
        {
            GUILayout.Label("Validate Cards", _header);
            _validateFolder = (DefaultAsset)EditorGUILayout.ObjectField("CardData Folder (optional)", _validateFolder, typeof(DefaultAsset), false);
            _validateDb = (CrimsonDynasty.CardDatabase)EditorGUILayout.ObjectField("Database (optional)", _validateDb, typeof(CrimsonDynasty.CardDatabase), false);

            if (GUILayout.Button("Run Validation"))
            {
                var cards = CollectFromSources(_validateDb, _validateFolder, null);
                _validateReport = ValidateCards(cards);
            }

            if (_validateReport is { Count: > 0 })
            {
                EditorGUILayout.Space(6);
                GUILayout.Label("Report", EditorStyles.boldLabel);
                foreach (var line in _validateReport)
                    EditorGUILayout.HelpBox(line, MessageType.Warning);
            }
        }

        private List<string> ValidateCards(List<CrimsonDynasty.CardData> cards)
        {
            var report = new List<string>();
            var nameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var c in cards)
            {
                if (!c) continue;
                if (string.IsNullOrWhiteSpace(c.CharacterName))
                    report.Add($"{c.name}: Name is empty.");
                if (string.IsNullOrWhiteSpace(c.Title))
                    report.Add($"{c.name}: Title is empty.");
                if (c.Portrait == null)
                    report.Add($"{c.name}: Portrait missing.");
                if (c.Age < 0 || c.Health < 0 || c.XP < 0 || c.InfluenceRate < 0)
                    report.Add($"{c.name}: Negative number in stats.");
                var key = $"{c.CharacterName}::{c.House}";
                if (!nameSet.Add(key))
                    report.Add($"{c.name}: Duplicate Name+House [{key}].");
            }

            if (report.Count == 0) report.Add("No issues found.");
            return report;
        }

        // =======================
        // TAB: IMPORT (basic CSV)
        // =======================
        private void DrawTabImport()
        {
            GUILayout.Label("CSV Import (Basic)", _header);
            _csv = (TextAsset)EditorGUILayout.ObjectField("CSV File", _csv, typeof(TextAsset), false);
            _importOutputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", _importOutputFolder, typeof(DefaultAsset), false);
            _importUpdateExisting = EditorGUILayout.Toggle("Update Existing", _importUpdateExisting);

            EditorGUILayout.HelpBox(
                "CSV header example:\n" +
                "Name,Title,House,Loyalty,Age,Health,SpecialAbility,XP,InfluenceRate,OathSlots,Status,Portrait\n" +
                "Jonn Stone,Wrongchild,Storm,Edward Storm,20,100,Swordman,200,0,\"Edward Storm\",\"Weak;Hated\",Jonn_Stone.png",
                MessageType.Info);

            if (GUILayout.Button("Import / Update"))
                ImportCsv();
        }

        private void ImportCsv()
        {
            if (_csv == null) { Dialog("Assign a CSV file."); return; }
            if (!ValidateFolder(_importOutputFolder, out string folderPath)) return;

            var lines = _csv.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length <= 1) { Dialog("CSV has no data rows."); return; }

            var header = SplitCsvLine(lines[0]);
            var map = HeaderMap(header);
            int created = 0, updated = 0;

            for (int i = 1; i < lines.Length; i++)
            {
                var row = SplitCsvLine(lines[i]);
                if (row.Count == 0) continue;

                string name = Get(map, row, "Name");
                string house = Get(map, row, "House");
                if (string.IsNullOrWhiteSpace(name)) continue;

                // find existing by Name+House
                var existing = FindCardByNameHouse(name, house);
                CrimsonDynasty.CardData card = existing;

                if (!existing)
                {
                    string safeName = MakeSafeName($"{name}_{house}");
                    string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folderPath, $"{safeName}.asset"));
                    card = ScriptableObject.CreateInstance<CrimsonDynasty.CardData>();
                    AssetDatabase.CreateAsset(card, path);
                    created++;
                }
                else if (!_importUpdateExisting) continue;
                else updated++;

                // write fields
                var so = new SerializedObject(card);
                so.FindProperty("characterName").stringValue = name;
                so.FindProperty("title").stringValue = Get(map, row, "Title");
                so.FindProperty("house").stringValue = house;
                so.FindProperty("loyalty").stringValue = Get(map, row, "Loyalty");
                so.FindProperty("age").intValue = ToInt(Get(map, row, "Age"));
                so.FindProperty("health").intValue = ToInt(Get(map, row, "Health"), 100);
                so.FindProperty("xp").intValue = ToInt(Get(map, row, "XP"));
                so.FindProperty("influenceRate").intValue = ToInt(Get(map, row, "InfluenceRate"));
                so.FindProperty("specialAbility").stringValue = Get(map, row, "SpecialAbility");

                SetList(so.FindProperty("oathSlots"), SplitList(Get(map, row, "OathSlots")));
                SetList(so.FindProperty("status"), SplitList(Get(map, row, "Status")));

                var portraitName = Get(map, row, "Portrait");
                if (!string.IsNullOrWhiteSpace(portraitName))
                {
                    var portrait = FindSpriteInProject(portraitName);
                    if (portrait) so.FindProperty("portrait").objectReferenceValue = portrait;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(card);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Dialog($"Import done. Created: {created}, Updated: {updated}");
        }

        // =======================
        // TAB: PREVIEW
        // =======================
        private void DrawTabPreview()
        {
            GUILayout.Label("Preview In Scene", _header);
            _previewDb = (CrimsonDynasty.CardDatabase)EditorGUILayout.ObjectField("Database (optional)", _previewDb, typeof(CrimsonDynasty.CardDatabase), false);
            _previewFolder = (DefaultAsset)EditorGUILayout.ObjectField("CardData Folder (optional)", _previewFolder, typeof(DefaultAsset), false);

            EditorGUILayout.LabelField("Explicit Cards (optional)");
            int remove = -1;
            for (int i = 0; i < _previewExplicit.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _previewExplicit[i] = (CrimsonDynasty.CardData)EditorGUILayout.ObjectField(_previewExplicit[i], typeof(CrimsonDynasty.CardData), false);
                    if (GUILayout.Button("X", GUILayout.Width(22))) remove = i;
                }
            }
            if (remove >= 0) _previewExplicit.RemoveAt(remove);
            if (GUILayout.Button("+ Add Card")) _previewExplicit.Add(null);

            if (GUILayout.Button("Spawn Row In Scene"))
            {
                // Require a base prefab for preview (reuse Prefabs tab selection if set)
                if (!_baseCardPrefab)
                {
                    Dialog("Go to Prefabs tab and assign a Base Card Prefab first.");
                    return;
                }
                _dbForPrefabs = _previewDb;
                _cardDataFolderForPrefabs = _previewFolder;
                _explicitCardsForPrefabs = _previewExplicit;
                SpawnPreviewFromSources();
            }
        }

        // =======================
        // Helpers
        // =======================
        private static bool ValidateFolder(DefaultAsset folder, out string path)
        {
            path = null;
            if (folder == null) { Dialog("Please assign an Output Folder."); return false; }
            path = AssetDatabase.GetAssetPath(folder);
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
            {
                Dialog("Output Folder is invalid."); return false;
            }
            return true;
        }

        private static void Dialog(string msg) => EditorUtility.DisplayDialog("Card Studio", msg, "OK");

        private static string MakeSafeName(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "Card";
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c.ToString(), "_");
            return s.Replace(' ', '_');
        }

        private static int ToInt(string s, int fallback = 0)
        {
            if (string.IsNullOrWhiteSpace(s)) return fallback;
            return int.TryParse(s, out var v) ? v : fallback;
        }

        private static List<string> SplitList(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return new List<string>();
            return s.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        }

        private static void SetList(SerializedProperty listProp, List<string> values)
        {
            listProp.ClearArray();
            for (int i = 0; i < values.Count; i++)
            {
                listProp.InsertArrayElementAtIndex(i);
                listProp.GetArrayElementAtIndex(i).stringValue = values[i];
            }
        }

        private static string Get(Dictionary<string, int> map, List<string> row, string col)
        {
            return map.TryGetValue(col, out var idx) && idx >= 0 && idx < row.Count ? row[idx] : "";
        }

        private static Dictionary<string, int> HeaderMap(List<string> header)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < header.Count; i++) map[header[i].Trim()] = i;
            return map;
        }

        private static List<string> SplitCsvLine(string line)
        {
            // Basic CSV splitter (handles quotes). Good enough for simple sheets.
            var result = new List<string>();
            if (line == null) return result;

            bool inQuotes = false;
            var cur = new System.Text.StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                    else inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(cur.ToString());
                    cur.Length = 0;
                }
                else cur.Append(c);
            }
            result.Add(cur.ToString());
            return result;
        }

        private static CrimsonDynasty.CardData FindCardByNameHouse(string name, string house)
        {
            string[] guids = AssetDatabase.FindAssets("t:CrimsonDynasty.CardData");
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var cd = AssetDatabase.LoadAssetAtPath<CrimsonDynasty.CardData>(path);
                if (cd && string.Equals(cd.CharacterName, name, StringComparison.OrdinalIgnoreCase)
                       && string.Equals(cd.House, house, StringComparison.OrdinalIgnoreCase))
                    return cd;
            }
            return null;
        }

        private static Sprite FindSpriteInProject(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;
            string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
            string[] guids = AssetDatabase.FindAssets($"{nameNoExt} t:Sprite");
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s && (string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(s.name, nameNoExt, StringComparison.OrdinalIgnoreCase)))
                    return s;
            }
            return null;
        }

        private static List<CrimsonDynasty.CardData> FindCardDataInFolder(DefaultAsset folder)
        {
            var list = new List<CrimsonDynasty.CardData>();
            if (!folder) return list;
            string folderPath = AssetDatabase.GetAssetPath(folder);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath)) return list;

            string[] guids = AssetDatabase.FindAssets("t:CrimsonDynasty.CardData", new[] { folderPath });
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var cd = AssetDatabase.LoadAssetAtPath<CrimsonDynasty.CardData>(path);
                if (cd) list.Add(cd);
            }
            return list;
        }

        private static List<CrimsonDynasty.CardData> CollectFromSources(
            CrimsonDynasty.CardDatabase db, DefaultAsset folder, List<CrimsonDynasty.CardData> explicitCards)
        {
            var pool = new List<CrimsonDynasty.CardData>();

            if (db && db.cards != null) pool.AddRange(db.cards.Where(c => c));
            if (folder) pool.AddRange(FindCardDataInFolder(folder));
            if (explicitCards != null) pool.AddRange(explicitCards.Where(c => c));

            return pool.Distinct().ToList();
        }
    }
}
#endif
