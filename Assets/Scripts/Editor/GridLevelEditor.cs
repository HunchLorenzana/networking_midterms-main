using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

public class GridLevelEditor : EditorWindow
{
    private enum ToolMode { Idle, Select, Place, Erase }

    [Serializable]
    private class PrefabEntry
    {
        public string category = "Obstacles";
        public GameObject prefab;
    }

    [SerializeField] private List<PrefabEntry> _prefabs = new List<PrefabEntry>();
    [SerializeField] private GameObject _levelRoot;

    [SerializeField] private ToolMode _toolMode = ToolMode.Idle;
    [SerializeField] private List<string> _palettes = new List<string> { "Obstacles", "Enemies", "Environment" };
    [SerializeField] private string _paletteTab = "Obstacles";
    [SerializeField] private bool _renamingPalette;
    [SerializeField] private string _paletteRenameBuffer = "";
    [SerializeField] private int _selectedPrefabIndex = -1;
    [SerializeField] private bool _showAssetPalette = true;
    [SerializeField] private float _prefabCardSize = 108f;

    [SerializeField] private float _gridSize = 1f;
    [SerializeField] private float _placementHeight;
    [SerializeField] private float _rotationY;
    [SerializeField] private float _gridExtent = 30f;
    [SerializeField] private float _placementLimitX = 20f;
    [SerializeField] private float _placementLimitZ = 20f;
    [SerializeField] private bool _mirrorX;
    [SerializeField] private bool _mirrorY;

    [SerializeField] private bool _showGrid = true;
    [SerializeField] private bool _showGhost = true;
    [SerializeField] private bool _showPlacementSettings = true;
    [SerializeField] private bool _showObjectManagement = true;

    private Vector2 _scroll;
    private Vector2 _paletteTabsScroll;
    private Vector3 _placementPosition;
    private bool _hasPlacementPosition;
    private bool _isPainting;
    private bool _isErasing;
    private Vector3 _lastPaintPosition;
    private bool _hasLastPaintPosition;

    private GameObject _ghostInstance;
    private readonly List<GameObject> _mirrorGhostInstances = new List<GameObject>();
    private GameObject _ghostSource;
    private Material _ghostMaterial;

    private int _pendingPickerIndex = -1;
    private int _pendingPickerControlId;

    private const float CardSize = 108f;
    private const float CardGap = 7f;

    private GUIStyle _titleStyle;
    private GUIStyle _subtitleStyle;
    private GUIStyle _sectionStyle;
    private GUIStyle _cardNameStyle;
    private GUIStyle _toolbarStyle;
    private GUIStyle _toolbarActiveStyle;
    private GUIStyle _mutedStyle;

    private static readonly Color Accent = new Color(0.20f, 0.58f, 0.88f);
    private static readonly Color GhostColor = new Color(0.15f, 0.85f, 1f, 0.38f);

    [MenuItem("Tools/3D Grid Level Editor")]
    public static void OpenWindow()
    {
        GridLevelEditor window = GetWindow<GridLevelEditor>("Level Builder");
        window.minSize = new Vector2(300f, 420f);
        window.Show();
    }

    private void OnEnable()
    {
        EnsurePalettes();
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.update += UpdateGhost;
    }

    private void EnsurePalettes()
    {
        if (_palettes == null)
            _palettes = new List<string>();

        if (_palettes.Count == 0)
            _palettes.Add("Obstacles");

        for (int i = 0; i < _palettes.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(_palettes[i]))
                _palettes[i] = "Palette " + (i + 1);

            _palettes[i] = _palettes[i].Trim();
        }

        if (string.IsNullOrWhiteSpace(_paletteTab) || !_palettes.Contains(_paletteTab))
            _paletteTab = _palettes[0];

        // Preserve the original built-in categories if a project already had slots.
        foreach (PrefabEntry entry in _prefabs)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.category))
                continue;

            string category = entry.category.Trim();
            if (!_palettes.Contains(category, StringComparer.OrdinalIgnoreCase))
                _palettes.Add(category);
        }
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.update -= UpdateGhost;
        DestroyGhost();
    }

    private void OnGUI()
    {
        HandlePrefabPickerResult();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawHeader();
        DrawToolBar();
        DrawPalette();
        DrawPlacementSettings();
        DrawObjectManagement();
        DrawSelectedObjectTools();
        DrawHelp();

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EnsureStyles();

        Color old = GUI.backgroundColor;
        GUI.backgroundColor = EditorGUIUtility.isProSkin
            ? new Color(0.19f, 0.22f, 0.27f)
            : new Color(0.86f, 0.90f, 0.95f);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();

        GUILayout.Label(EditorGUIUtility.IconContent("Grid.BoxTool"), GUILayout.Width(28f), GUILayout.Height(30f));

        EditorGUILayout.BeginVertical();
        GUILayout.Label("LEVEL BUILDER", _titleStyle);
        GUILayout.Label("3D grid placement workspace", _subtitleStyle);
        EditorGUILayout.EndVertical();

        GUILayout.FlexibleSpace();

        string modeLabel = _toolMode == ToolMode.Idle ? "NORMAL" :
            _toolMode == ToolMode.Select ? "SELECT" :
            _toolMode == ToolMode.Place ? "PLACE" : "ERASE";
        GUIStyle modeStyle = new GUIStyle(EditorStyles.miniButton);
        modeStyle.fontStyle = FontStyle.Bold;
        modeStyle.alignment = TextAnchor.MiddleCenter;
        modeStyle.fixedWidth = 70f;
        modeStyle.fixedHeight = 24f;

        Color oldContent = GUI.contentColor;
        GUI.contentColor = _toolMode == ToolMode.Idle ? Color.gray : Accent;
        GUILayout.Button(modeLabel, modeStyle);
        GUI.contentColor = oldContent;

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        GUI.backgroundColor = old;
        EditorGUILayout.Space(4f);
    }

    private void DrawToolBar()
    {
        EnsureStyles();

        EditorGUILayout.BeginHorizontal();
        DrawModeButton("Normal", ToolMode.Idle, Color.white);
        DrawModeButton("Select", ToolMode.Select, new Color(0.45f, 0.75f, 0.55f));
        DrawModeButton("Place", ToolMode.Place, Accent);
        DrawModeButton("Erase", ToolMode.Erase, new Color(0.88f, 0.30f, 0.25f));
        if (GUILayout.Button("Quick Switch (Q)", GUILayout.Height(31f), GUILayout.Width(112f)))
        {
            Undo.RecordObject(this, "Switch Placement Tool");
            _toolMode = _toolMode == ToolMode.Place ? ToolMode.Erase : ToolMode.Place;
            _isPainting = false;
            _isErasing = false;
            _hasLastPaintPosition = false;
            if (_toolMode == ToolMode.Place) RefreshGhost(); else DestroyGhost();
            EditorUtility.SetDirty(this);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5f);
    }

    private void DrawModeButton(string label, ToolMode mode, Color activeColor)
    {
        EnsureStyles();
        bool active = _toolMode == mode;

        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = active
            ? (mode == ToolMode.Idle ? new Color(0.70f, 0.73f, 0.78f) : activeColor)
            : (EditorGUIUtility.isProSkin ? new Color(0.28f, 0.30f, 0.34f) : new Color(0.93f, 0.94f, 0.96f));

        GUIStyle style = active ? _toolbarActiveStyle : _toolbarStyle;
        if (GUILayout.Button(label, style, GUILayout.Height(31f)))
        {
            Undo.RecordObject(this, "Change Level Editor Mode");
            _toolMode = active ? ToolMode.Idle : mode;
            _isPainting = false;
            _hasLastPaintPosition = false;

            if (_toolMode != ToolMode.Place)
                DestroyGhost();
            else
                RefreshGhost();

            EditorUtility.SetDirty(this);
        }

        GUI.backgroundColor = previous;
    }

    private void DrawPalette()
    {
        EnsurePalettes();
        EnsureStyles();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        _showAssetPalette = EditorGUILayout.Foldout(_showAssetPalette, "ASSET PALETTE", true);
        GUILayout.FlexibleSpace();
        if (_showAssetPalette)
            GUILayout.Label("Choose a prefab to place", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        if (!_showAssetPalette)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.BeginHorizontal();

        // Palette tabs scroll horizontally instead of pushing the whole editor wider.
        _paletteTabsScroll = EditorGUILayout.BeginScrollView(
            _paletteTabsScroll,
            false,
            false,
            GUI.skin.horizontalScrollbar,
            GUI.skin.verticalScrollbar,
            GUIStyle.none,
            GUILayout.Height(27f)
        );

        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < _palettes.Count; i++)
            DrawTabButton(_palettes[i]);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();

        GUILayout.Space(3f);
        if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(27f), GUILayout.Height(25f)))
            AddPalette();

        if (GUILayout.Button("Rename", EditorStyles.toolbarButton, GUILayout.Width(55f), GUILayout.Height(25f)))
        {
            _renamingPalette = !_renamingPalette;
            _paletteRenameBuffer = _paletteTab;
        }

        if (GUILayout.Button("−", EditorStyles.toolbarButton, GUILayout.Width(27f), GUILayout.Height(25f)))
            DeleteCurrentPalette();

        EditorGUILayout.EndHorizontal();

        if (_renamingPalette)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label("Palette name", GUILayout.Width(75f));
            _paletteRenameBuffer = EditorGUILayout.TextField(_paletteRenameBuffer);
            if (GUILayout.Button("Apply", GUILayout.Width(52f)))
                RenameCurrentPalette();
            if (GUILayout.Button("Cancel", GUILayout.Width(52f)))
                _renamingPalette = false;
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(5f);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Card Size", GUILayout.Width(65f));
        float newCardSize = EditorGUILayout.Slider(_prefabCardSize, 64f, 150f);
        if (!Mathf.Approximately(newCardSize, _prefabCardSize))
        {
            _prefabCardSize = newCardSize;
            Repaint();
        }
        GUILayout.Label(Mathf.RoundToInt(_prefabCardSize) + " px", GUILayout.Width(42f));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5f);

        // EditorGUILayout help-box margins add a little width around each card.
        // Reserve room for the scroll view and panel padding to avoid clipped columns.
        float cardSize = _prefabCardSize;
        float usableWidth = Mathf.Max(cardSize + 16f, position.width - 48f);
        float layoutCardWidth = cardSize + 18f;
        int columns = Mathf.Max(1, Mathf.FloorToInt((usableWidth + CardGap) / (layoutCardWidth + CardGap)));
        int visible = 0;

        EditorGUILayout.BeginVertical();
        for (int i = 0; i < _prefabs.Count; i++)
        {
            PrefabEntry entry = _prefabs[i];
            if (entry == null || !IsInCurrentTab(entry))
                continue;

            if (visible % columns == 0)
                EditorGUILayout.BeginHorizontal();

            DrawPrefabCard(i);
            visible++;

            if (visible % columns == 0)
                EditorGUILayout.EndHorizontal();
        }

        if (visible == 0)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(5f);
            GUILayout.Label(EditorGUIUtility.IconContent("Prefab Icon"), GUILayout.Height(28f));
            GUILayout.Label("This palette is empty", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Drag prefab assets from the Project window into the drop area below.", _mutedStyle);
            GUILayout.Space(5f);
            EditorGUILayout.EndVertical();
        }
        else if (visible % columns != 0)
        {
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6f);
        DrawPrefabDropZone();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4f);
    }

    private void DrawPrefabDropZone()
    {
        Rect dropRect = GUILayoutUtility.GetRect(0f, 42f, GUILayout.ExpandWidth(true));
        GUI.Box(dropRect, GUIContent.none, EditorStyles.helpBox);
        GUI.Label(dropRect, "＋  DROP PREFABS HERE TO ADD TO THIS PALETTE", EditorStyles.centeredGreyMiniLabel);

        Event currentEvent = Event.current;
        if (!dropRect.Contains(currentEvent.mousePosition))
            return;

        if (currentEvent.type == EventType.DragUpdated || currentEvent.type == EventType.DragPerform)
        {
            bool hasPrefab = false;
            foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
            {
                GameObject draggedPrefab = draggedObject as GameObject;
                if (draggedPrefab != null && PrefabUtility.IsPartOfPrefabAsset(draggedPrefab))
                {
                    hasPrefab = true;
                    break;
                }
            }

            DragAndDrop.visualMode = hasPrefab ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

            if (currentEvent.type == EventType.DragPerform && hasPrefab)
            {
                DragAndDrop.AcceptDrag();
                Undo.RecordObject(this, "Add Prefabs to Palette");

                foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
                {
                    GameObject draggedPrefab = draggedObject as GameObject;
                    if (draggedPrefab == null || !PrefabUtility.IsPartOfPrefabAsset(draggedPrefab))
                        continue;

                    _prefabs.Add(new PrefabEntry
                    {
                        category = _paletteTab,
                        prefab = draggedPrefab
                    });
                }

                EditorUtility.SetDirty(this);
                Repaint();
            }

            currentEvent.Use();
        }
    }

    private void DrawTabButton(string label)
    {
        bool selected = string.Equals(_paletteTab, label, StringComparison.OrdinalIgnoreCase);
        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = selected
            ? Accent
            : (EditorGUIUtility.isProSkin ? new Color(0.30f, 0.32f, 0.36f) : new Color(0.90f, 0.92f, 0.95f));

        GUIStyle style = new GUIStyle(EditorStyles.toolbarButton);
        style.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
        style.padding = new RectOffset(8, 8, 3, 3);

        if (GUILayout.Button(label, style, GUILayout.Height(25f), GUILayout.MinWidth(78f), GUILayout.MaxWidth(150f)))
        {
            Undo.RecordObject(this, "Change Prefab Palette");
            _paletteTab = label;

            if (!HasValidPrefabSelection() || !IsInCurrentTab(_prefabs[_selectedPrefabIndex]))
            {
                _selectedPrefabIndex = -1;
                if (_toolMode == ToolMode.Place)
                    _toolMode = ToolMode.Idle;
            }

            RefreshGhost();
            EditorUtility.SetDirty(this);
        }

        GUI.backgroundColor = previous;
    }

    private void RenameCurrentPalette()
    {
        string newName = string.IsNullOrWhiteSpace(_paletteRenameBuffer)
            ? "Palette"
            : _paletteRenameBuffer.Trim();

        if (string.Equals(newName, _paletteTab, StringComparison.OrdinalIgnoreCase))
        {
            _renamingPalette = false;
            return;
        }

        if (_palettes.Contains(newName, StringComparer.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("Palette Name Already Exists", "Choose a different name for this palette.", "OK");
            return;
        }

        string oldName = _paletteTab;
        Undo.RecordObject(this, "Rename Palette");

        int paletteIndex = _palettes.FindIndex(name =>
            string.Equals(name, oldName, StringComparison.OrdinalIgnoreCase));
        if (paletteIndex >= 0)
            _palettes[paletteIndex] = newName;

        foreach (PrefabEntry entry in _prefabs)
        {
            if (entry != null && string.Equals(entry.category, oldName, StringComparison.OrdinalIgnoreCase))
                entry.category = newName;
        }

        _paletteTab = newName;
        _paletteRenameBuffer = newName;
        _renamingPalette = false;
        EditorUtility.SetDirty(this);
        Repaint();
    }

    private void AddPalette()
    {
        Undo.RecordObject(this, "Add Palette");

        string baseName = "New Palette";
        string paletteName = baseName;
        int suffix = 1;

        while (_palettes.Contains(paletteName, StringComparer.OrdinalIgnoreCase))
        {
            paletteName = baseName + " " + suffix;
            suffix++;
        }

        _palettes.Add(paletteName);
        _paletteTab = paletteName;
        _selectedPrefabIndex = -1;

        if (_toolMode == ToolMode.Place)
            _toolMode = ToolMode.Idle;

        DestroyGhost();
        EditorUtility.SetDirty(this);
        Repaint();
    }

    private void DeleteCurrentPalette()
    {
        if (_palettes.Count <= 1)
        {
            EditorUtility.DisplayDialog("Cannot Delete Palette", "The editor must have at least one palette.", "OK");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Delete Palette",
            "Delete palette '" + _paletteTab + "' and all prefab slots inside it?",
            "Delete",
            "Cancel"
        );

        if (!confirmed)
            return;

        Undo.RecordObject(this, "Delete Palette");

        _prefabs.RemoveAll(entry => entry != null &&
            string.Equals(entry.category, _paletteTab, StringComparison.OrdinalIgnoreCase));

        _palettes.RemoveAll(name => string.Equals(name, _paletteTab, StringComparison.OrdinalIgnoreCase));
        _paletteTab = _palettes[0];
        _selectedPrefabIndex = -1;

        if (_toolMode == ToolMode.Place)
            _toolMode = ToolMode.Idle;

        DestroyGhost();
        EditorUtility.SetDirty(this);
        Repaint();
    }

    private bool IsInCurrentTab(PrefabEntry entry)
    {
        string category = string.IsNullOrWhiteSpace(entry.category) ? "Obstacles" : entry.category.Trim();
        return string.Equals(category, _paletteTab, StringComparison.OrdinalIgnoreCase);
    }

    private void DrawPrefabCard(int index)
    {
        PrefabEntry entry = _prefabs[index];
        bool selected = _selectedPrefabIndex == index && _toolMode == ToolMode.Place;
        EnsureStyles();

        Color oldBackground = GUI.backgroundColor;
        GUI.backgroundColor = selected
            ? new Color(0.20f, 0.48f, 0.70f)
            : (EditorGUIUtility.isProSkin ? new Color(0.25f, 0.27f, 0.30f) : new Color(0.90f, 0.91f, 0.93f));

        float cardSize = _prefabCardSize;
        float previewHeight = cardSize * 0.78f;
        float cardHeight = previewHeight + 49f;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(cardSize + 10f), GUILayout.Height(cardHeight));

        Rect previewRect = GUILayoutUtility.GetRect(cardSize, previewHeight, GUILayout.Width(cardSize));
        GUI.Box(previewRect, GUIContent.none, EditorStyles.helpBox);
        if (GUI.Button(previewRect, GUIContent.none, GUIStyle.none))
            ActivatePrefabCard(index);

        if (entry.prefab != null)
        {
            Texture2D preview = AssetPreview.GetAssetPreview(entry.prefab);
            if (preview == null)
                preview = AssetPreview.GetMiniThumbnail(entry.prefab);

            if (preview != null)
                GUI.DrawTexture(
                    new Rect(previewRect.x + 4f, previewRect.y + 4f, previewRect.width - 8f, previewRect.height - 8f),
                    preview,
                    ScaleMode.ScaleToFit
                );
            else
                GUI.Label(previewRect, "Loading preview…", EditorStyles.centeredGreyMiniLabel);
        }
        else
        {
            GUI.Label(previewRect, "+", EditorStyles.centeredGreyMiniLabel);
        }

        string cardName = entry.prefab != null ? entry.prefab.name : "Empty Slot";
        GUIStyle nameStyle = new GUIStyle(_cardNameStyle);
        nameStyle.normal.textColor = selected ? Color.white : GUI.skin.label.normal.textColor;
        nameStyle.alignment = TextAnchor.MiddleCenter;

        if (GUILayout.Button(cardName, nameStyle, GUILayout.Height(22f)))
            ActivatePrefabCard(index);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button(entry.prefab == null ? "Assign Prefab +" : "Change", EditorStyles.miniButton, GUILayout.Height(20f)))
            OpenPrefabPicker(index);

        if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(23f), GUILayout.Height(20f)))
        {
            Undo.RecordObject(this, "Remove Prefab Slot");
            if (_selectedPrefabIndex == index)
            {
                _selectedPrefabIndex = -1;
                _toolMode = ToolMode.Idle;
                DestroyGhost();
            }
            _prefabs.RemoveAt(index);
            EditorUtility.SetDirty(this);
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        GUI.backgroundColor = oldBackground;
    }

    private void ActivatePrefabCard(int index)
    {
        if (index < 0 || index >= _prefabs.Count)
            return;

        Undo.RecordObject(this, "Select Prefab");
        PrefabEntry entry = _prefabs[index];
        if (_selectedPrefabIndex == index && _toolMode == ToolMode.Place)
        {
            _selectedPrefabIndex = -1;
            _toolMode = ToolMode.Idle;
            DestroyGhost();
        }
        else if (entry.prefab != null)
        {
            _selectedPrefabIndex = index;
            _toolMode = ToolMode.Place;
            RefreshGhost();
        }
        else
        {
            OpenPrefabPicker(index);
        }

        EditorUtility.SetDirty(this);
        SceneView.RepaintAll();
        Repaint();
    }

    private void OpenPrefabPicker(int index)
    {
        if (index < 0 || index >= _prefabs.Count)
            return;

        _pendingPickerIndex = index;
        _pendingPickerControlId = GUIUtility.GetControlID(FocusType.Passive);

        EditorGUIUtility.ShowObjectPicker<GameObject>(
            _prefabs[index].prefab,
            false,
            "",
            _pendingPickerControlId
        );
    }

    private void HandlePrefabPickerResult()
    {
        if (Event.current.type != EventType.ExecuteCommand &&
            Event.current.type != EventType.ValidateCommand)
            return;

        if (Event.current.commandName != "ObjectSelectorUpdated" &&
            Event.current.commandName != "ObjectSelectorClosed")
            return;

        if (_pendingPickerIndex < 0 || _pendingPickerIndex >= _prefabs.Count)
            return;

        if (EditorGUIUtility.GetObjectPickerControlID() != _pendingPickerControlId)
            return;

        if (Event.current.commandName == "ObjectSelectorUpdated")
        {
            GameObject picked = EditorGUIUtility.GetObjectPickerObject() as GameObject;
            if (picked != null)
            {
                Undo.RecordObject(this, "Assign Prefab");
                _prefabs[_pendingPickerIndex].prefab = picked;
                EditorUtility.SetDirty(this);

                _selectedPrefabIndex = _pendingPickerIndex;
                _toolMode = ToolMode.Place;
                RefreshGhost();
            }
        }

        if (Event.current.commandName == "ObjectSelectorClosed")
            _pendingPickerIndex = -1;

        Repaint();
    }

    private void EnsureStyles()
    {
        if (_titleStyle != null)
            return;

        _titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold
        };

        _subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 10
        };

        _sectionStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(0, 0, 2, 2)
        };

        _cardNameStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            clipping = TextClipping.Ellipsis,
            wordWrap = false
        };

        _toolbarStyle = new GUIStyle(EditorStyles.toolbarButton)
        {
            fontSize = 11,
            fontStyle = FontStyle.Normal
        };

        _toolbarActiveStyle = new GUIStyle(EditorStyles.toolbarButton)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold
        };

        _mutedStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };
    }

    private void DrawSectionHeading(string title, string subtitle = null)
    {
        EnsureStyles();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(title, _sectionStyle);
        GUILayout.FlexibleSpace();
        if (!string.IsNullOrEmpty(subtitle))
            GUILayout.Label(subtitle, _mutedStyle);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawPlacementSettings()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        _showPlacementSettings = EditorGUILayout.Foldout(_showPlacementSettings, "PLACEMENT SETTINGS", true);
        GUILayout.FlexibleSpace();
        if (_showPlacementSettings)
            GUILayout.Label("GRID & TRANSFORM", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        if (_showPlacementSettings)
        {
            EditorGUILayout.Space(4f);
            DrawUndoableToggle("Show Grid", ref _showGrid);
            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("MIRROR PLACEMENT / ERASE", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            DrawUndoableToggle("Mirror X", ref _mirrorX);
            DrawUndoableToggle("Mirror Y", ref _mirrorY);
            EditorGUILayout.EndHorizontal();
            DrawGhostToggle();

            DrawUndoableFloat("Snap Size", ref _gridSize, 0.01f, 100f);
            DrawUndoableFloat("Height (Y)", ref _placementHeight, -10000f, 10000f);
            DrawUndoableFloat("Grid Extent", ref _gridExtent, 1f, 500f);
            EditorGUILayout.LabelField("PLACEMENT BOUNDS (ROOT LOCAL)", EditorStyles.miniBoldLabel);
            DrawUndoableFloat("Limit X (+/−)", ref _placementLimitX, 0.5f, 500f);
            DrawUndoableFloat("Limit Z (+/−)", ref _placementLimitZ, 0.5f, 500f);

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("SNAP PRESETS", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            DrawSnapPreset("0.25", 0.25f);
            DrawSnapPreset("0.5", 0.5f);
            DrawSnapPreset("1", 1f);
            DrawSnapPreset("2", 2f);
            DrawSnapPreset("4", 4f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Rotation", GUILayout.Width(55));

            if (GUILayout.Button("−90°", GUILayout.Height(23f)))
                ChangeRotation(-90f);

            if (GUILayout.Button("+90°", GUILayout.Height(23f)))
                ChangeRotation(90f);

            DrawUndoableFloat("Y", ref _rotationY, -36000f, 36000f, GUILayout.MinWidth(60f));
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawUndoableToggle(string label, ref bool value)
    {
        bool newValue = EditorGUILayout.Toggle(label, value);
        if (newValue == value)
            return;

        Undo.RecordObject(this, "Change " + label);
        value = newValue;
        EditorUtility.SetDirty(this);
        SceneView.RepaintAll();
    }

    private void DrawGhostToggle()
    {
        bool newValue = EditorGUILayout.Toggle("Ghost Preview", _showGhost);
        if (newValue == _showGhost)
            return;

        Undo.RecordObject(this, "Change Ghost Preview");
        _showGhost = newValue;

        if (_showGhost && _toolMode == ToolMode.Place)
            RefreshGhost();
        else
            DestroyGhost();

        EditorUtility.SetDirty(this);
        SceneView.RepaintAll();
    }

    private void DrawUndoableFloat(string label, ref float value, float min, float max, params GUILayoutOption[] options)
    {
        float newValue = EditorGUILayout.FloatField(label, value, options);
        newValue = Mathf.Clamp(newValue, min, max);

        if (Mathf.Approximately(newValue, value))
            return;

        Undo.RecordObject(this, "Change " + label);
        value = newValue;
        EditorUtility.SetDirty(this);
        SceneView.RepaintAll();
        UpdateGhostTransform();
    }

    private void DrawSnapPreset(string label, float value)
    {
        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = Mathf.Approximately(_gridSize, value) ? Accent : Color.white;

        if (GUILayout.Button(label, GUILayout.Height(23)) && !Mathf.Approximately(_gridSize, value))
        {
            Undo.RecordObject(this, "Change Snap Size");
            _gridSize = value;
            EditorUtility.SetDirty(this);
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = previous;
    }

    private void DrawObjectManagement()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        _showObjectManagement = EditorGUILayout.Foldout(_showObjectManagement, "OBJECT MANAGEMENT", true);
        GUILayout.FlexibleSpace();
        if (_showObjectManagement)
            GUILayout.Label("SCENE HIERARCHY", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        if (_showObjectManagement)
        {
            EditorGUILayout.Space(4f);
            _levelRoot = (GameObject)EditorGUILayout.ObjectField("Level Root", _levelRoot, typeof(GameObject), true);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Level Root"))
                CreateLevelRoot();
            if (GUILayout.Button("Focus Grid"))
                FocusGrid();
            EditorGUILayout.EndHorizontal();

            if (_levelRoot != null)
            {
                int placedCount = _levelRoot.transform.childCount;
                EditorGUILayout.LabelField("Placed objects: " + placedCount, EditorStyles.miniLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select Level Root"))
                    Selection.activeGameObject = _levelRoot;

                GUI.enabled = placedCount > 0;
                Color previousColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.75f, 0.28f, 0.25f);
                if (GUILayout.Button("Clear All", GUILayout.Height(24f)))
                    ClearAllPlacedObjects();
                GUI.backgroundColor = previousColor;
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void ClearAllPlacedObjects()
    {
        if (_levelRoot == null)
            return;

        Transform root = _levelRoot.transform;
        int childCount = root.childCount;
        if (childCount == 0)
            return;

        bool confirmed = EditorUtility.DisplayDialog(
            "Clear All Placed Objects",
            "Delete all " + childCount + " direct child object(s) under '" + _levelRoot.name + "'?\n\nThis can be undone with Ctrl+Z.",
            "Clear All",
            "Cancel");

        if (!confirmed)
            return;

        Scene targetScene = _levelRoot.scene;
        Undo.RegisterFullObjectHierarchyUndo(_levelRoot, "Clear All Placed Objects");

        // Delete from the end so child indices remain valid.
        for (int i = root.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);

        if (targetScene.IsValid())
            EditorSceneManager.MarkSceneDirty(targetScene);

        Selection.objects = Array.Empty<UnityEngine.Object>();
        Repaint();
        SceneView.RepaintAll();
    }

    private void DrawHelp()
    {
        EditorGUILayout.HelpBox(
            "Scene View controls:\n" +
            "• Place mode: hold left mouse and drag to place repeatedly\n" +
            "• Erase mode: hold left mouse and drag across placed objects to delete them\n" +
            "• Select mode: click; Shift-click to multi-select\n" +
            "• Q: quick-switch between Place and Erase  • Esc: Normal\n" +
            "• Mirror X/Y: reflect placement and erase across grid axes\n" +
            "• Orange rectangle: customizable placement/erase limits\n" +
            "• R: rotate selected object/prefab by 90°\n" +
            "• Alt + mouse: orbit/pan Scene View\n\n" +
            "Use + / − beside the palette tabs to create or delete palettes. " +
            "Undo (Ctrl+Z) restores placement and editor setting changes.",
            MessageType.None
        );
    }

    private void CreateLevelRoot()
    {
        GameObject root = new GameObject("LevelObjects");
        Undo.RegisterCreatedObjectUndo(root, "Create Level Root");

        Undo.RecordObject(this, "Assign Level Root");
        _levelRoot = root;

        EditorUtility.SetDirty(this);
        EditorSceneManager.MarkSceneDirty(root.scene);
    }

    private void FocusGrid()
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null)
            return;

        view.pivot = GetGridOriginWorld() + GetGridUp() * _placementHeight;
        view.size = Mathf.Max(10f, _gridExtent);
        view.Repaint();
    }

    private void ChangeRotation(float delta)
    {
        Undo.RecordObject(this, "Change Placement Rotation");
        _rotationY = Mathf.Repeat(_rotationY + delta, 360f);
        EditorUtility.SetDirty(this);
        UpdateGhostTransform();
        SceneView.RepaintAll();
    }

    private Transform GridTransform => _levelRoot != null ? _levelRoot.transform : null;

    private Vector3 GetGridOriginWorld()
    {
        return GridTransform != null ? GridTransform.position : Vector3.zero;
    }

    private Vector3 GetGridUp()
    {
        return GridTransform != null ? GridTransform.up : Vector3.up;
    }

    private Vector3 WorldToGridLocal(Vector3 worldPosition)
    {
        return GridTransform != null ? GridTransform.InverseTransformPoint(worldPosition) : worldPosition;
    }

    private Vector3 GridLocalToWorld(Vector3 localPosition)
    {
        return GridTransform != null ? GridTransform.TransformPoint(localPosition) : localPosition;
    }

    private Vector3 Snap(Vector3 worldPosition)
    {
        Vector3 local = WorldToGridLocal(worldPosition);
        float step = Mathf.Max(0.01f, _gridSize);
        local.x = Mathf.Round(local.x / step) * step;
        local.z = Mathf.Round(local.z / step) * step;
        local.y = _placementHeight;
        return GridLocalToWorld(local);
    }

    private Vector3 GetPlacementPlanePoint()
    {
        return GridLocalToWorld(new Vector3(0f, _placementHeight, 0f));
    }

    private bool IsWithinPlacementBounds(Vector3 worldPosition)
    {
        Vector3 local = WorldToGridLocal(worldPosition);
        return Mathf.Abs(local.x) <= _placementLimitX + 0.0001f &&
               Mathf.Abs(local.z) <= _placementLimitZ + 0.0001f;
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;

        if (_showGrid && (_toolMode == ToolMode.Place || _toolMode == ToolMode.Erase || _toolMode == ToolMode.Select))
            DrawGrid();

        if (_toolMode == ToolMode.Idle)
        {
            DestroyGhost();
            return;
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        Plane placementPlane = new Plane(GetGridUp(), GetPlacementPlanePoint());
        _hasPlacementPosition = placementPlane.Raycast(ray, out float distance);

        if (_hasPlacementPosition)
        {
            _placementPosition = Snap(ray.GetPoint(distance));
            if (_toolMode == ToolMode.Place && _showGhost && HasValidPrefabSelection())
            {
                EnsureGhost();
                UpdateGhostTransform();
            }
            else
            {
                DestroyGhost();
            }
        }

        HandleSceneClick(e);

        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            sceneView.Repaint();
    }

    private void DrawGrid()
    {
        float step = Mathf.Max(0.01f, _gridSize);
        int countX = Mathf.Min(Mathf.CeilToInt(_gridExtent / step), 200);
        int countZ = countX;
        float drawX = Mathf.Min(_gridExtent, _placementLimitX);
        float drawZ = Mathf.Min(_gridExtent, _placementLimitZ);

        Matrix4x4 oldMatrix = Handles.matrix;
        Handles.matrix = GridTransform != null ? GridTransform.localToWorldMatrix : Matrix4x4.identity;
        Handles.color = new Color(0.40f, 0.52f, 0.65f, 0.25f);

        for (int i = -countX; i <= countX; i++)
        {
            float x = i * step;
            if (Mathf.Abs(x) <= drawX + step * 0.5f)
                Handles.DrawLine(new Vector3(x, _placementHeight, -drawZ), new Vector3(x, _placementHeight, drawZ));
        }

        for (int i = -countZ; i <= countZ; i++)
        {
            float z = i * step;
            if (Mathf.Abs(z) <= drawZ + step * 0.5f)
                Handles.DrawLine(new Vector3(-drawX, _placementHeight, z), new Vector3(drawX, _placementHeight, z));
        }

        // The bold rectangle is the actual placement/erase limit.
        Handles.color = new Color(1f, 0.65f, 0.12f, 0.95f);
        Vector3 a = new Vector3(-_placementLimitX, _placementHeight, -_placementLimitZ);
        Vector3 b = new Vector3(_placementLimitX, _placementHeight, -_placementLimitZ);
        Vector3 c = new Vector3(_placementLimitX, _placementHeight, _placementLimitZ);
        Vector3 d = new Vector3(-_placementLimitX, _placementHeight, _placementLimitZ);
        Handles.DrawAAPolyLine(3f, a, b, c, d, a);

        Handles.color = new Color(0.12f, 0.72f, 1f, 0.85f);
        Handles.DrawLine(new Vector3(-drawX, _placementHeight, 0f), new Vector3(drawX, _placementHeight, 0f));
        Handles.DrawLine(new Vector3(0f, _placementHeight, -drawZ), new Vector3(0f, _placementHeight, drawZ));
        Handles.matrix = oldMatrix;
        Handles.color = Color.white;

        // Show whether the current cursor cell is inside the placement boundary.
        if (_hasPlacementPosition)
        {
            Handles.color = IsWithinPlacementBounds(_placementPosition) ? new Color(0.2f, 1f, 0.3f, 0.9f) : Color.red;
            Handles.DrawWireCube(_placementPosition, Vector3.one * Mathf.Max(0.15f, _gridSize * 0.9f));
            Handles.color = Color.white;
        }
    }

    private void HandleSceneClick(Event e)
    {
        // Q quickly toggles between Place and Erase. Select has no keybind.
        if (e.type == EventType.KeyDown && !e.alt && !e.control && !e.command)
        {
            if (e.keyCode == KeyCode.Q && (_toolMode == ToolMode.Place || _toolMode == ToolMode.Erase))
            {
                SetToolModeFromScene(_toolMode == ToolMode.Place ? ToolMode.Erase : ToolMode.Place);
                e.Use();
                return;
            }

            if (e.keyCode == KeyCode.Escape)
            {
                SetToolModeFromScene(ToolMode.Idle);
                e.Use();
                return;
            }

            if (e.keyCode == KeyCode.R)
            {
                if (_toolMode == ToolMode.Place)
                {
                    ChangeRotation(90f);
                    e.Use();
                    return;
                }

                if (_toolMode == ToolMode.Select && GetSelectedPlacedObject() != null)
                {
                    RotateSelectedObject(90f);
                    e.Use();
                    return;
                }
            }
        }

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            if (_toolMode == ToolMode.Select)
            {
                SelectPlacedObject(e.mousePosition, e.shift);
                e.Use();
                return;
            }

            if (_toolMode == ToolMode.Place && _hasPlacementPosition)
            {
                _isPainting = true;
                _isErasing = false;
                _hasLastPaintPosition = false;
                PaintAtCurrentPosition();
                e.Use();
                return;
            }

            if (_toolMode == ToolMode.Erase)
            {
                _isErasing = true;
                _isPainting = false;
                EraseAtMirroredPositions(e.mousePosition);
                e.Use();
                return;
            }
        }

        // While held, place another copy whenever the pointer enters a new grid cell.
        if (_toolMode == ToolMode.Place && _isPainting &&
            (e.type == EventType.MouseDrag || e.type == EventType.MouseMove) &&
            (e.button == 0 || e.type == EventType.MouseMove) &&
            e.alt == false)
        {
            if (e.type == EventType.MouseDrag && e.button == 0)
                PaintAtCurrentPosition();

            e.Use();
        }

        // Erase continuously while the left mouse button is held and dragged.
        if (_toolMode == ToolMode.Erase && _isErasing &&
            e.type == EventType.MouseDrag && e.button == 0 && !e.alt)
        {
            EraseAtMirroredPositions(e.mousePosition);
            e.Use();
        }

        if (e.type == EventType.MouseUp && e.button == 0)
        {
            _isPainting = false;
            _isErasing = false;
            _hasLastPaintPosition = false;
        }
    }

    private void SetToolModeFromScene(ToolMode mode)
    {
        Undo.RecordObject(this, "Change Level Editor Mode");
        _toolMode = mode;
        _isPainting = false;
        _isErasing = false;
        _hasLastPaintPosition = false;

        if (_toolMode == ToolMode.Place)
            RefreshGhost();
        else
            DestroyGhost();

        EditorUtility.SetDirty(this);
        Repaint();
        SceneView.RepaintAll();
    }

    private void PaintAtCurrentPosition()
    {
        if (!_hasPlacementPosition || !HasValidPrefabSelection())
            return;

        // Avoid stacking duplicate objects in the same snapped cell while dragging.
        if (_hasLastPaintPosition && Vector3.Distance(_lastPaintPosition, _placementPosition) < 0.001f)
            return;

        PlaceAtMirroredPositions(_placementPosition);
        _lastPaintPosition = _placementPosition;
        _hasLastPaintPosition = true;
    }

    private void SelectPlacedObject(Vector2 mousePosition, bool additive)
    {
        GameObject picked = HandleUtility.PickGameObject(mousePosition, false);

        if (picked == null)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 10000f))
                picked = hit.collider.gameObject;
        }

        if (picked == null)
        {
            if (!additive)
                Selection.objects = Array.Empty<UnityEngine.Object>();
            return;
        }

        Transform placedRoot = _levelRoot != null
            ? FindDirectChildUnderLevelRoot(picked.transform)
            : picked.transform;

        GameObject target = placedRoot != null ? placedRoot.gameObject : picked;

        if (!additive)
        {
            Selection.objects = new UnityEngine.Object[] { target };
            return;
        }

        // Shift-click toggles this object in the current multi-selection.
        List<UnityEngine.Object> selection = new List<UnityEngine.Object>(Selection.objects);
        int existingIndex = selection.IndexOf(target);

        if (existingIndex >= 0)
            selection.RemoveAt(existingIndex);
        else
            selection.Add(target);

        Selection.objects = selection.ToArray();
        Selection.activeGameObject = selection.Count > 0 ? selection[selection.Count - 1] as GameObject : null;
    }

    private GameObject GetSelectedPlacedObject()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
            return null;

        if (_levelRoot == null)
            return selected;

        Transform placedRoot = FindDirectChildUnderLevelRoot(selected.transform);
        return placedRoot != null ? placedRoot.gameObject : null;
    }

    private void DrawSelectedObjectTools()
    {
        GameObject selected = GetSelectedPlacedObject();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("SELECTED OBJECT", EditorStyles.boldLabel);

        if (selected == null)
        {
            EditorGUILayout.LabelField("Switch to Select mode and click a placed object.", EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.LabelField(selected.name, EditorStyles.miniBoldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Rotate −90°", GUILayout.Height(26f)))
                RotateSelectedObject(-90f);

            if (GUILayout.Button("Rotate +90°", GUILayout.Height(26f)))
                RotateSelectedObject(90f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Rotation", GUILayout.Height(23f)))
            {
                Undo.RecordObject(selected.transform, "Reset Object Rotation");
                selected.transform.rotation = Quaternion.identity;
                EditorSceneManager.MarkSceneDirty(selected.scene);
            }

            if (GUILayout.Button("Focus Selected", GUILayout.Height(23f)))
            {
                Selection.activeGameObject = selected;
                SceneView.lastActiveSceneView?.FrameSelected();
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void RotateSelectedObject(float degrees)
    {
        GameObject selected = GetSelectedPlacedObject();
        if (selected == null)
            return;

        Undo.RecordObject(selected.transform, "Rotate Level Object");
        selected.transform.rotation = Quaternion.AngleAxis(degrees, Vector3.up) * selected.transform.rotation;
        EditorSceneManager.MarkSceneDirty(selected.scene);
        SceneView.RepaintAll();
        Repaint();
    }

    private bool HasValidPrefabSelection()
    {
        return _selectedPrefabIndex >= 0 &&
               _selectedPrefabIndex < _prefabs.Count &&
               _prefabs[_selectedPrefabIndex] != null &&
               _prefabs[_selectedPrefabIndex].prefab != null;
    }

    private void PlaceAtMirroredPositions(Vector3 basePosition)
    {
        Vector3 localBase = WorldToGridLocal(basePosition);
        HashSet<Vector3> localPositions = new HashSet<Vector3>();
        localPositions.Add(localBase);
        if (_mirrorX) localPositions.Add(new Vector3(-localBase.x, localBase.y, localBase.z));
        if (_mirrorY) localPositions.Add(new Vector3(localBase.x, localBase.y, -localBase.z));
        if (_mirrorX && _mirrorY) localPositions.Add(new Vector3(-localBase.x, localBase.y, -localBase.z));

        // Treat a mirrored set as one operation: if any target cell is blocked,
        // skip the whole set rather than leaving only one side placed.
        List<Vector3> worldPositions = new List<Vector3>();
        foreach (Vector3 localPosition in localPositions)
        {
            Vector3 worldPosition = GridLocalToWorld(localPosition);
            if (!IsWithinPlacementBounds(worldPosition) || IsGridPositionOccupied(worldPosition))
            {
                _placementPosition = basePosition;
                return;
            }
            worldPositions.Add(worldPosition);
        }

        foreach (Vector3 worldPosition in worldPositions)
        {
            _placementPosition = worldPosition;
            PlacePrefab();
        }
        _placementPosition = basePosition;
    }

    private void EraseAtMirroredPositions(Vector2 mousePosition)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        Plane plane = new Plane(GetGridUp(), GetPlacementPlanePoint());
        if (!plane.Raycast(ray, out float distance)) return;

        Vector3 basePosition = Snap(ray.GetPoint(distance));
        Vector3 localBase = WorldToGridLocal(basePosition);
        HashSet<Vector3> localPositions = new HashSet<Vector3>();
        localPositions.Add(localBase);
        if (_mirrorX) localPositions.Add(new Vector3(-localBase.x, localBase.y, localBase.z));
        if (_mirrorY) localPositions.Add(new Vector3(localBase.x, localBase.y, -localBase.z));
        if (_mirrorX && _mirrorY) localPositions.Add(new Vector3(-localBase.x, localBase.y, -localBase.z));

        foreach (Vector3 localPosition in localPositions)
        {
            Vector3 worldPosition = GridLocalToWorld(localPosition);
            if (!IsWithinPlacementBounds(worldPosition))
                continue;

            // Erase by snapped grid cell, not by projecting a point to the
            // screen and picking whatever happens to be in front of it.
            ErasePlacedObjectAtGridPosition(worldPosition);
        }
    }


    private void ErasePlacedObjectAtGridPosition(Vector3 worldPosition)
    {
        if (_levelRoot == null)
            return;

        const float tolerance = 0.001f;
        for (int i = _levelRoot.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = _levelRoot.transform.GetChild(i);
            if (child == null)
                continue;

            Vector3 childGridLocal = WorldToGridLocal(child.position);
            Vector3 targetGridLocal = WorldToGridLocal(worldPosition);
            childGridLocal.x = Mathf.Round(childGridLocal.x / Mathf.Max(0.01f, _gridSize)) * Mathf.Max(0.01f, _gridSize);
            childGridLocal.z = Mathf.Round(childGridLocal.z / Mathf.Max(0.01f, _gridSize)) * Mathf.Max(0.01f, _gridSize);
            targetGridLocal.x = Mathf.Round(targetGridLocal.x / Mathf.Max(0.01f, _gridSize)) * Mathf.Max(0.01f, _gridSize);
            targetGridLocal.z = Mathf.Round(targetGridLocal.z / Mathf.Max(0.01f, _gridSize)) * Mathf.Max(0.01f, _gridSize);

            if (Mathf.Abs(childGridLocal.x - targetGridLocal.x) < tolerance &&
                Mathf.Abs(childGridLocal.z - targetGridLocal.z) < tolerance)
            {
                Scene targetScene = child.gameObject.scene;
                Undo.DestroyObjectImmediate(child.gameObject);
                if (targetScene.IsValid())
                    EditorSceneManager.MarkSceneDirty(targetScene);
                return;
            }
        }
    }

    private void PlacePrefab()
    {
        if (!HasValidPrefabSelection())
            return;

        // A grid cell can contain only one placed object. This also prevents
        // duplicate placements when multi-selecting objects or mirroring.
        if (!IsWithinPlacementBounds(_placementPosition) || IsGridPositionOccupied(_placementPosition))
            return;

        GameObject prefab = _prefabs[_selectedPrefabIndex].prefab;
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
            return;

        Undo.RegisterCreatedObjectUndo(instance, "Place Prefab");
        instance.transform.position = _placementPosition;
        instance.transform.rotation = prefab.transform.rotation * Quaternion.Euler(0f, _rotationY, 0f);

        if (_levelRoot != null)
            Undo.SetTransformParent(instance.transform, _levelRoot.transform, "Parent Level Object");

        Selection.activeGameObject = instance;
        EditorSceneManager.MarkSceneDirty(instance.scene);
    }


    private bool IsGridPositionOccupied(Vector3 position)
    {
        if (_levelRoot == null)
            return false;

        const float tolerance = 0.001f;
        for (int i = 0; i < _levelRoot.transform.childCount; i++)
        {
            Transform child = _levelRoot.transform.GetChild(i);
            if (child == null)
                continue;

            Vector3 existingPosition = Snap(child.position);
            if (Mathf.Abs(existingPosition.x - position.x) < tolerance &&
                Mathf.Abs(existingPosition.y - position.y) < tolerance &&
                Mathf.Abs(existingPosition.z - position.z) < tolerance)
                return true;
        }

        return false;
    }

    private void EraseAtMouse(Vector2 mousePosition)
    {
        GameObject picked = HandleUtility.PickGameObject(mousePosition, false);

        if (picked == null)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 10000f))
                picked = hit.collider.gameObject;
        }

        if (picked == null || _levelRoot == null)
            return;

        Transform placedRoot = FindDirectChildUnderLevelRoot(picked.transform);
        if (placedRoot == null)
            return;

        // Save scene before destroy. Unity invalidates the target immediately.
        Scene targetScene = placedRoot.gameObject.scene;
        GameObject targetObject = placedRoot.gameObject;

        Undo.DestroyObjectImmediate(targetObject);

        if (targetScene.IsValid())
            EditorSceneManager.MarkSceneDirty(targetScene);
    }

    private Transform FindDirectChildUnderLevelRoot(Transform candidate)
    {
        if (_levelRoot == null || candidate == null)
            return null;

        Transform rootTransform = _levelRoot.transform;
        Transform current = candidate;

        while (current != null && current != rootTransform)
        {
            Transform parent = current.parent;

            if (parent == rootTransform)
                return current;

            current = parent;
        }

        return null;
    }

    private void EnsureGhost()
    {
        if (!HasValidPrefabSelection())
        {
            DestroyGhost();
            return;
        }

        GameObject prefab = _prefabs[_selectedPrefabIndex].prefab;

        if (_ghostInstance != null && _ghostSource == prefab)
            return;

        DestroyGhost();

        _ghostSource = prefab;
        _ghostInstance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (_ghostInstance == null)
            return;

        _ghostInstance.name = "[Level Editor Ghost] " + prefab.name;
        _ghostInstance.hideFlags = HideFlags.HideAndDontSave;

        foreach (MonoBehaviour behaviour in _ghostInstance.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour != null) behaviour.enabled = false;

        foreach (Collider collider in _ghostInstance.GetComponentsInChildren<Collider>(true))
            if (collider != null) collider.enabled = false;

        foreach (Rigidbody body in _ghostInstance.GetComponentsInChildren<Rigidbody>(true))
            if (body != null) body.isKinematic = true;

        CreateGhostMaterial();
        ApplyGhostMaterial();
        UpdateGhostTransform();
    }

    private void CreateGhostMaterial()
    {
        if (_ghostMaterial != null)
            return;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return;

        _ghostMaterial = new Material(shader);
        _ghostMaterial.name = "Level Builder Ghost";
        _ghostMaterial.hideFlags = HideFlags.HideAndDontSave;

        if (_ghostMaterial.HasProperty("_BaseColor"))
            _ghostMaterial.SetColor("_BaseColor", GhostColor);
        if (_ghostMaterial.HasProperty("_Color"))
            _ghostMaterial.SetColor("_Color", GhostColor);

        if (_ghostMaterial.HasProperty("_Surface"))
            _ghostMaterial.SetFloat("_Surface", 1f);
        if (_ghostMaterial.HasProperty("_ZWrite"))
            _ghostMaterial.SetFloat("_ZWrite", 0f);
        if (_ghostMaterial.HasProperty("_SrcBlend"))
            _ghostMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (_ghostMaterial.HasProperty("_DstBlend"))
            _ghostMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        _ghostMaterial.SetOverrideTag("RenderType", "Transparent");
        _ghostMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        _ghostMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    private void ApplyGhostMaterial()
    {
        if (_ghostInstance == null || _ghostMaterial == null)
            return;

        foreach (Renderer renderer in _ghostInstance.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;

            Material[] replacements = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < replacements.Length; i++)
                replacements[i] = _ghostMaterial;

            renderer.sharedMaterials = replacements;
        }
    }

    private void UpdateGhost()
    {
        if (_ghostInstance == null)
            return;

        if (!_showGhost || _toolMode != ToolMode.Place || !HasValidPrefabSelection())
        {
            DestroyGhost();
            return;
        }

        if (_hasPlacementPosition)
            UpdateGhostTransform();
    }

    private void UpdateGhostTransform()
    {
        if (_ghostInstance == null || !_hasPlacementPosition || !HasValidPrefabSelection())
            return;

        GameObject prefab = _prefabs[_selectedPrefabIndex].prefab;
        Quaternion ghostRotation = prefab.transform.rotation * Quaternion.Euler(0f, _rotationY, 0f);

        // Main preview follows the cursor.
        _ghostInstance.transform.position = _placementPosition;
        _ghostInstance.transform.rotation = ghostRotation;

        // Mirror previews use the same root-relative coordinates as placement.
        Vector3 localBase = WorldToGridLocal(_placementPosition);
        HashSet<Vector3> localPositions = new HashSet<Vector3>();
        localPositions.Add(localBase);
        if (_mirrorX) localPositions.Add(new Vector3(-localBase.x, localBase.y, localBase.z));
        if (_mirrorY) localPositions.Add(new Vector3(localBase.x, localBase.y, -localBase.z));
        if (_mirrorX && _mirrorY) localPositions.Add(new Vector3(-localBase.x, localBase.y, -localBase.z));

        List<Vector3> mirroredWorldPositions = new List<Vector3>();
        foreach (Vector3 localPosition in localPositions)
        {
            Vector3 worldPosition = GridLocalToWorld(localPosition);
            if (Vector3.Distance(worldPosition, _placementPosition) < 0.001f)
                continue;
            if (IsWithinPlacementBounds(worldPosition))
                mirroredWorldPositions.Add(worldPosition);
        }

        EnsureMirrorGhostCount(mirroredWorldPositions.Count, prefab);
        for (int i = 0; i < mirroredWorldPositions.Count; i++)
        {
            _mirrorGhostInstances[i].transform.position = mirroredWorldPositions[i];
            _mirrorGhostInstances[i].transform.rotation = ghostRotation;
        }
    }

    private void EnsureMirrorGhostCount(int requiredCount, GameObject prefab)
    {
        while (_mirrorGhostInstances.Count > requiredCount)
        {
            GameObject last = _mirrorGhostInstances[_mirrorGhostInstances.Count - 1];
            _mirrorGhostInstances.RemoveAt(_mirrorGhostInstances.Count - 1);
            if (last != null)
                DestroyImmediate(last);
        }

        while (_mirrorGhostInstances.Count < requiredCount)
        {
            GameObject mirrorGhost = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (mirrorGhost == null)
                break;

            mirrorGhost.name = "[Level Editor Mirror Ghost] " + prefab.name;
            mirrorGhost.hideFlags = HideFlags.HideAndDontSave;
            foreach (MonoBehaviour behaviour in mirrorGhost.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null) behaviour.enabled = false;
            foreach (Collider collider in mirrorGhost.GetComponentsInChildren<Collider>(true))
                if (collider != null) collider.enabled = false;
            foreach (Rigidbody body in mirrorGhost.GetComponentsInChildren<Rigidbody>(true))
                if (body != null) body.isKinematic = true;

            CreateGhostMaterial();
            foreach (Renderer renderer in mirrorGhost.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                Material[] replacements = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < replacements.Length; i++) replacements[i] = _ghostMaterial;
                renderer.sharedMaterials = replacements;
            }
            _mirrorGhostInstances.Add(mirrorGhost);
        }
    }

    private void RefreshGhost()
    {
        DestroyGhost();

        if (_toolMode == ToolMode.Place && _showGhost && HasValidPrefabSelection())
            EnsureGhost();

        SceneView.RepaintAll();
    }

    private void DestroyGhost()
    {
        if (_ghostInstance != null)
        {
            DestroyImmediate(_ghostInstance);
            _ghostInstance = null;
        }

        for (int i = _mirrorGhostInstances.Count - 1; i >= 0; i--)
            if (_mirrorGhostInstances[i] != null)
                DestroyImmediate(_mirrorGhostInstances[i]);
        _mirrorGhostInstances.Clear();

        _ghostSource = null;

        if (_ghostMaterial != null)
        {
            DestroyImmediate(_ghostMaterial);
            _ghostMaterial = null;
        }
    }
}
