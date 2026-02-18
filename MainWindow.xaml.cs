using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Text.RegularExpressions;
using Sm64DecompLevelViewer.Models;
using Sm64DecompLevelViewer.Services;
using Sm64DecompLevelViewer.Rendering;
using OpenTK.Windowing.Desktop;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;

namespace Sm64DecompLevelViewer;

public partial class MainWindow : Window
{
    private List<LevelMetadata> _levels = new();
    private readonly LevelScanner _levelScanner;
    private readonly CollisionParser _collisionParser;
    private readonly ModelParser _modelParser;
    private LevelMetadata? _selectedLevel;
    private Dictionary<string, string> _collisionFiles = new();
    private Dictionary<string, List<string>> _modelFiles = new();
    private readonly MacroObjectParser _macroParser;
    private CollisionMesh? _currentCollisionMesh;
    private VisualMesh? _currentVisualMesh;
    private GeometryRenderer? _renderer;
    private string? _projectRootPath;

    private static readonly Regex SpecialObjectPattern = new Regex(
        @"SPECIAL_OBJECT\s*\(\s*/\*preset\*/\s*([^,]+),\s*/\*pos\*/\s*(-?\d+),\s*(-?\d+),\s*(-?\d+)\s*\)",
        RegexOptions.Compiled
    );

    private static readonly Regex SpecialObjectWithYawPattern = new Regex(
        @"SPECIAL_OBJECT_WITH_YAW\s*\(\s*/\*preset\*/\s*([^,]+),\s*/\*pos\*/\s*(-?\d+),\s*(-?\d+),\s*(-?\d+),\s*/\*yaw\*/\s*(-?\d+)\s*\)",
        RegexOptions.Compiled
    );

    private static readonly Regex SpecialObjectWithYawAndParamPattern = new Regex(
        @"SPECIAL_OBJECT_WITH_YAW_AND_PARAM\s*\(\s*/\*preset\*/\s*([^,]+),\s*/\*pos\*/\s*(-?\d+),\s*(-?\d+),\s*(-?\d+),\s*/\*yaw\*/\s*(-?\d+),\s*/\*bhvParam2\*/\s*([^)]+)\)",
        RegexOptions.Compiled
    );

    public MainWindow()
    {
        
        InitializeComponent();
        _levelScanner = new LevelScanner();
        _collisionParser = new CollisionParser();
        _modelParser = new ModelParser();
        _macroParser = new MacroObjectParser();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Please select an SM64 Project Folder to start";
    }

    private void Click_LoadLevel(object sender, RoutedEventArgs e)
    {
        LoadLevels();
    }

    private void SelectFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select SM64 Project Root Folder",
            InitialDirectory = _projectRootPath ?? AppDomain.CurrentDomain.BaseDirectory
        };

        if (dialog.ShowDialog() == true)
        {
            var selectedPath = dialog.FolderName;
            
            var dirName = Path.GetFileName(selectedPath);
            if (dirName.Equals("levels", StringComparison.OrdinalIgnoreCase) || 
                dirName.Equals("howtomake", StringComparison.OrdinalIgnoreCase))
            {
                selectedPath = Path.GetDirectoryName(selectedPath) ?? selectedPath;
            }

            _projectRootPath = selectedPath;
            LoadLevels();
        }
    }

    private void LoadLevels()
    {
        if (string.IsNullOrEmpty(_projectRootPath) || !Directory.Exists(_projectRootPath))
        {
            return;
        }

        try
        {
            _levels = new List<LevelMetadata>();

            var levelsDir = Path.Combine(_projectRootPath, "levels");
            if (!Directory.Exists(levelsDir)) levelsDir = Path.Combine(_projectRootPath, "howtomake", "levels");

            if (Directory.Exists(levelsDir))
            {
                _levels = _levelScanner.ScanLevels(Path.GetDirectoryName(levelsDir)!);
            }

            if (_levels.Count == 0)
            {
                StatusText.Text = "No levels found";
                MessageBox.Show(
                    "No level.yaml files were found in the howtomake/levels directory.",
                    "No Levels Found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var groupedLevels = CollectionViewSource.GetDefaultView(_levels);
            groupedLevels.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
            
            LevelListBox.ItemsSource = groupedLevels;

            StatusText.Text = $"{_levels.Count} levels loaded";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Error loading levels";
            MessageBox.Show(
                $"An error occurred while loading levels:\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void LevelListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (LevelListBox.SelectedItem is LevelMetadata level)
        {
            DisplayLevelDetails(level);
        }
        else
        {
            HideLevelDetails();
        }
    }

    private void DisplayLevelDetails(LevelMetadata level)
    {
        _selectedLevel = level;
        NoSelectionText.Visibility = Visibility.Collapsed;
        LevelDetailsPanel.Visibility = Visibility.Visible;

        FullNameText.Text = level.FullName;
        ShortNameText.Text = level.ShortName;
        CategoryText.Text = level.Category;
        AreaCountText.Text = level.AreaCount.ToString();
        SkyboxText.Text = level.SkyboxBin ?? "(none)";
        TextureBinText.Text = level.TextureBin;
        EffectsText.Text = level.Effects ? "Yes" : "No";
        
        ObjectsText.Text = level.Objects.Count > 0 
            ? string.Join(", ", level.Objects) 
            : "(none)";
        
        ActorBinsText.Text = level.ActorBins.Count > 0 
            ? string.Join(", ", level.ActorBins) 
            : "(none)";
        
        CommonBinText.Text = level.CommonBin.Count > 0 
            ? string.Join(", ", level.CommonBin) 
            : "(none)";
        
        LevelPathText.Text = level.LevelPath;

        LoadCollisionFiles(level);

        StatusText.Text = $"Selected: {level.FullName}";
    }

    private void HideLevelDetails()
    {
        NoSelectionText.Visibility = Visibility.Visible;
        LevelDetailsPanel.Visibility = Visibility.Collapsed;
        StatusText.Text = $"{_levels.Count} levels loaded";
    }

    private void LoadCollisionFiles(LevelMetadata level)
    {
        _collisionFiles = _collisionParser.FindCollisionFiles(level.LevelPath);
        _modelFiles = _modelParser.FindModelFiles(level.LevelPath);
        
        AreaComboBox.Items.Clear();
        CollisionStatsText.Text = "";
        View3DButton.IsEnabled = false;
        _currentCollisionMesh = null;
        _currentVisualMesh = null;

        if (_collisionFiles.Count > 0)
        {
            foreach (var areaName in _collisionFiles.Keys.OrderBy(k => k))
            {
                AreaComboBox.Items.Add(areaName);
            }
            
            AreaComboBox.SelectedIndex = 0;
        }
        else
        {
            CollisionStatsText.Text = "No collision files found for this level.";
        }
    }

    private void AreaComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (AreaComboBox.SelectedItem is string areaName && _selectedLevel != null)
        {
            LoadCollisionMesh(areaName);
        }
    }

    private void LoadCollisionMesh(string areaName)
    {
        if (!_collisionFiles.ContainsKey(areaName) || _selectedLevel == null)
            return;

        var collisionFilePath = _collisionFiles[areaName];
        _currentCollisionMesh = _collisionParser.ParseCollisionFile(
            collisionFilePath, 
            areaName, 
            _selectedLevel.FullName
        );

        if (_modelFiles.ContainsKey(areaName))
        {
            var modelFilePaths = _modelFiles[areaName];
            
            Dictionary<string, OpenTK.Mathematics.Matrix4>? transforms = null;
            // The geo.inc.c is usually in the area directory (e.g., levels/bob/areas/1/geo.inc.c)
            var areaPath = Path.GetDirectoryName(modelFilePaths[0]);
            if (areaPath != null)
            {
                var geoLayoutPath = Path.Combine(areaPath, "..", "geo.inc.c");
                if (File.Exists(geoLayoutPath))
                {
                    Console.WriteLine($"Parsing geometry layout: {geoLayoutPath}");
                    var geoParser = new GeoLayoutParser();
                    var geoRoot = geoParser.ParseGeoLayout(geoLayoutPath);
                    if (geoRoot != null)
                    {
                        transforms = geoParser.ExtractTransformations(geoRoot);
                    }
                }
            }

                if (transforms == null) transforms = new Dictionary<string, OpenTK.Mathematics.Matrix4>();

                var levelRoot = Path.GetDirectoryName(Path.GetDirectoryName(modelFilePaths[0]));
                if (levelRoot != null)
                {
                    var specialParser = new SpecialObjectParser();
                    var includePath = Path.Combine(_selectedLevel.LevelPath, "..", "..", "include", "special_presets.inc.c");
                    var presetMapping = specialParser.ParsePresets(includePath);

                    var objectParser = new ObjectParser();
                    var scriptPath = Path.Combine(_selectedLevel.LevelPath, "script.c");
                    var modelToGeo = objectParser.ParseLoadModels(scriptPath);

                    string colContent = File.ReadAllText(collisionFilePath);
                    var geoParser = new GeoLayoutParser();

                    var specialMatches = SpecialObjectPattern.Matches(colContent);
                    var specialWithYawMatches = SpecialObjectWithYawPattern.Matches(colContent);

                    void ProcessSpecial(string preset, float x, float y, float z, float yaw)
                    {
                        if (presetMapping.TryGetValue(preset, out string? modelId))
                        {
                            if (modelToGeo.TryGetValue(modelId, out string? geoLayoutName))
                            {
                                // Find geo.inc.c for this model
                                string? subGeoPath = FindGeoLayoutForModel(levelRoot, geoLayoutName);
                                if (subGeoPath != null)
                                {
                                    string? dlName = geoParser.GetPrimaryDisplayListName(subGeoPath);
                                    if (dlName != null)
                                    {
                                        var mat = OpenTK.Mathematics.Matrix4.CreateRotationY(OpenTK.Mathematics.MathHelper.DegreesToRadians(yaw)) *
                                                  OpenTK.Mathematics.Matrix4.CreateTranslation(x, y, z);
                                        transforms[dlName] = mat;
                                        Console.WriteLine($"Mapped Special Object {preset} -> {dlName} at ({x},{y},{z})");
                                    }
                                }
                            }
                        }
                    }

                    foreach (Match m in specialMatches)
                        ProcessSpecial(m.Groups[1].Value, float.Parse(m.Groups[2].Value), float.Parse(m.Groups[3].Value), float.Parse(m.Groups[4].Value), 0);
                    foreach (Match m in specialWithYawMatches)
                        ProcessSpecial(m.Groups[1].Value, float.Parse(m.Groups[2].Value), float.Parse(m.Groups[3].Value), float.Parse(m.Groups[4].Value), (float.Parse(m.Groups[5].Value) / 65536.0f) * 360.0f);
                }
                
                _currentVisualMesh = _modelParser.ParseMultipleModelFiles(
                modelFilePaths,
                areaName,
                _selectedLevel.FullName,
                transforms
            );
        }
        else
        {
            _currentVisualMesh = null;
        }

        // Update UI
        if (_currentCollisionMesh != null)
        {
            string statsText = $"✓ Collision: {_currentCollisionMesh.VertexCount} vertices, {_currentCollisionMesh.TriangleCount} triangles";
            
            if (_currentVisualMesh != null)
            {
                statsText += $"\n✓ Visual: {_currentVisualMesh.VertexCount} vertices, {_currentVisualMesh.TriangleCount} triangles";
            }
            
            CollisionStatsText.Text = statsText;
            View3DButton.IsEnabled = true;
        }
        else
        {
            CollisionStatsText.Text = "✗ Failed to parse collision file.";
            View3DButton.IsEnabled = false;
        }
    }

    private void View3DButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentCollisionMesh == null)
            return;

        try
        {
            var nativeWindowSettings = new NativeWindowSettings()
            {
                Size = new Vector2i(1024, 768),
                Title = $"3D Collision Viewer - {_currentCollisionMesh.LevelName} - {_currentCollisionMesh.AreaName}",
                APIVersion = new Version(3, 3),
            };

            var gameWindowSettings = new GameWindowSettings()
            {
                UpdateFrequency = 60.0
            };

            // Create and show renderer window
            _renderer = new GeometryRenderer(gameWindowSettings, nativeWindowSettings);
            _renderer.LoadMesh(_currentCollisionMesh);
            
            _renderer.ObjectSelected += OnObjectSelected;
            
            if (_currentVisualMesh != null)
            {
                _renderer.LoadVisualMesh(_currentVisualMesh);
            }

            if (_selectedLevel != null)
            {
                var scriptPath = Path.Combine(_selectedLevel.LevelPath, "script.c");
                if (File.Exists(scriptPath))
                {
                    var objectParser = new ObjectParser();
                    var objects = objectParser.ParseScriptFile(scriptPath);

                    try
                    {
                        var macroListName = objectParser.ParseMacroListName(scriptPath);
                        if (macroListName != null)
                        {
                            Dictionary<string, MacroPreset> presets = new();
                            
                            if (!string.IsNullOrEmpty(_projectRootPath))
                            {
                                var selectedPresetsPath = Path.Combine(_projectRootPath, "include", "macro_presets.inc.c");
                                if (!File.Exists(selectedPresetsPath)) selectedPresetsPath = Path.Combine(_projectRootPath, "howtomake", "include", "macro_presets.inc.c");

                                if (File.Exists(selectedPresetsPath))
                                {
                                    presets = _macroParser.ParsePresets(selectedPresetsPath);
                                }
                            }

                            var levelPath = _selectedLevel.LevelPath;
                            var macroFiles = Directory.GetFiles(levelPath, "macro.inc.c", SearchOption.AllDirectories);
                            
                            foreach (var macroFile in macroFiles)
                            {
                                if (File.ReadAllText(macroFile).Contains(macroListName))
                                {
                                    var macroObjects = _macroParser.ParseMacroFile(macroFile, presets);
                                    Console.WriteLine($"Adding {macroObjects.Count} macro objects from {macroFile}");
                                    objects.AddRange(macroObjects);
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading macro objects: {ex.Message}");
                    }
                    // -------------------------------

                    try
                    {
                        var areaName = AreaComboBox.SelectedItem as string;
                        if (areaName != null && _collisionFiles.TryGetValue(areaName, out var collisionFilePath))
                        {
                            var specialParser = new SpecialObjectParser();
                            Dictionary<string, string> presetMapping = new();

                            if (!string.IsNullOrEmpty(_projectRootPath))
                            {
                                var selectedIncludePath = Path.Combine(_projectRootPath, "include", "special_presets.inc.c");
                                if (!File.Exists(selectedIncludePath)) selectedIncludePath = Path.Combine(_projectRootPath, "howtomake", "include", "special_presets.inc.c");

                                if (File.Exists(selectedIncludePath))
                                {
                                    presetMapping = specialParser.ParsePresets(selectedIncludePath);
                                }
                            }
                            
                            string colContent = File.ReadAllText(collisionFilePath);
                            
                            var specialMatches = SpecialObjectPattern.Matches(colContent);
                            var specialWithYawMatches = SpecialObjectWithYawPattern.Matches(colContent);
                            var specialWithYawAndParamMatches = SpecialObjectWithYawAndParamPattern.Matches(colContent);

                            foreach (Match m in specialMatches)
                            {
                                string preset = m.Groups[1].Value.Trim();
                                if (presetMapping.TryGetValue(preset, out string? modelId))
                                {
                                    objects.Add(new LevelObject {
                                        ModelName = modelId,
                                        Behavior = "(Special Object)",
                                        X = int.Parse(m.Groups[2].Value),
                                        Y = int.Parse(m.Groups[3].Value),
                                        Z = int.Parse(m.Groups[4].Value),
                                        RY = 0
                                    });
                                }
                            }

                            foreach (Match m in specialWithYawMatches)
                            {
                                string preset = m.Groups[1].Value.Trim();
                                if (presetMapping.TryGetValue(preset, out string? modelId))
                                {
                                    objects.Add(new LevelObject {
                                        ModelName = modelId,
                                        Behavior = "(Special Object)",
                                        X = int.Parse(m.Groups[2].Value),
                                        Y = int.Parse(m.Groups[3].Value),
                                        Z = int.Parse(m.Groups[4].Value),
                                        RY = (int)((float.Parse(m.Groups[5].Value) / 256.0f) * 360.0f)
                                    });
                                }
                            }

                            foreach (Match m in specialWithYawAndParamMatches)
                            {
                                string preset = m.Groups[1].Value.Trim();
                                if (presetMapping.TryGetValue(preset, out string? modelId))
                                {
                                    string paramStr = m.Groups[6].Value.Trim();
                                    uint paramValue = 0;
                                    if (paramStr.StartsWith("0x")) paramValue = Convert.ToUInt32(paramStr, 16);
                                    else uint.TryParse(paramStr, out paramValue);

                                    objects.Add(new LevelObject {
                                        ModelName = modelId,
                                        Behavior = "(Special Object)",
                                        X = int.Parse(m.Groups[2].Value),
                                        Y = int.Parse(m.Groups[3].Value),
                                        Z = int.Parse(m.Groups[4].Value),
                                        RY = (int)((float.Parse(m.Groups[5].Value) / 256.0f) * 360.0f),
                                        Params = paramValue
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading special objects: {ex.Message}");
                    }
                    // ------------------------------------------------

                    _renderer?.SetObjects(objects);
                }
            }
            
            _renderer.Run();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error opening 3D viewer:\n{ex.Message}",
                "3D Viewer Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnObjectSelected(LevelObject? selectedObject)
    {
        Dispatcher.Invoke(() =>
        {
            if (selectedObject != null)
            {
                ObjectInfoText.Text = $"Selected: {selectedObject.ModelName} | Pos: ({selectedObject.X:F0}, {selectedObject.Y:F0}, {selectedObject.Z:F0}) | Rot: ({selectedObject.RX}, {selectedObject.RY}, {selectedObject.RZ}) | Behavior: {selectedObject.Behavior}";
            }
            else
            {
                ObjectInfoText.Text = "";
            }
        });
    }

    private string? FindGeoLayoutForModel(string areaPath, string geoLayoutName)
    {
        try
        {
            var geoFiles = Directory.GetFiles(areaPath, "geo.inc.c", SearchOption.AllDirectories);
            foreach (var file in geoFiles)
            {
                if (File.ReadAllText(file).Contains(geoLayoutName))
                    return file;
            }
        }
        catch { }
        return null;
    }
}

