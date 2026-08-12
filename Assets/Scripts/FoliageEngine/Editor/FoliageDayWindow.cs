using UnityEngine;
using UnityEditor;
using System.Linq;
using Color = UnityEngine.Color;
using System.Collections.Generic;
using System;


namespace PWTA
{
    public class FoliageRetrievePopup : EditorWindow
    {
        public List<Transform> objects = new List<Transform>();
        public System.Action<bool, List<Transform>> onClose;
        public Vector2 scroll = Vector2.zero;



        public static void Show(System.Action<bool, List<Transform>> onClose)
        {
            var window = EditorWindow.GetWindow(typeof(FoliageRetrievePopup), false, "풀 수집") as FoliageRetrievePopup;
            window.onClose = onClose;
            window.Show();
        }

        public void OnGUI()
        {
            const float BottomBarHeight = 40f;
            // 전체 영역
            var fullRect = new Rect(0, 0, position.width, position.height);

            // 하단 버튼 영역 제외한 콘텐츠 영역
            var contentRect = new Rect(
                0,
                0,
                position.width,
                position.height - BottomBarHeight
            );

            // -------------------------
            // Content
            // -------------------------
            GUILayout.BeginArea(contentRect);
            {
                EditorGUILayout.LabelField("프리팹 루트 할당", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                scroll = EditorGUILayout.BeginScrollView(scroll);
                {
                    int removeIndex = -1;

                    for (int i = 0; i < objects.Count; i++)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            objects[i] = EditorGUILayout.ObjectField(objects[i] as Transform, typeof(Transform), true) as Transform;
                            if (GUILayout.Button("X", GUILayout.Width(20)))
                                removeIndex = i;
                        }
                    }

                    if (removeIndex >= 0)
                        objects.RemoveAt(removeIndex);

                    EditorGUILayout.Space(6);

                    if (GUILayout.Button("+ 루트 추가", GUILayout.Height(22)))
                        objects.Add(null);
                }
                EditorGUILayout.EndScrollView();
            }
            GUILayout.EndArea();

            // -------------------------
            // Bottom Bar
            // -------------------------
            var bottomRect = new Rect(
                0,
                position.height - BottomBarHeight,
                position.width,
                BottomBarHeight
            );

            GUILayout.BeginArea(bottomRect, EditorStyles.helpBox);
            {
                GUILayout.FlexibleSpace();

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("취소", GUILayout.Width(80)))
                    {
                        onClose?.Invoke(false, null);
                        Close();
                    }

                    if (GUILayout.Button("적용", GUILayout.Width(80)))
                    {
                        onClose?.Invoke(true, objects);
                        Close();
                    }
                }

                GUILayout.FlexibleSpace();
            }
            GUILayout.EndArea();
        }
    }

    [ExecuteInEditMode]
    public class FoliageDayWindow : EditorWindow
    {
        public enum eEditMode
        {
            Select,
            Paint,
            Erase,
        }
        public static string Title = "";
        private eEditMode _editMode = eEditMode.Paint;
        public Transform _terrainRoot;
        private FoliageManager _foliageManager;
        private FoliagePaletteAsset _paletteAsset;
        public FoliageBrush _brush;
        private Texture[] buttonTextures;
        private GUISkin _guiSkin;
        private GUIStyle[] _guiStyles;
        private GUIContent[] _guidGridContents;
        private GUIContent[] _guiButtonContents;
        private List<FoliageViewEditor> _previewPlantList = new List<FoliageViewEditor>();

        private float _radius = 1.5f;
        private float _overwrapRatio = 0.5f;
        private float _eraseDensity = 1f;
        private float _mixtureRate = 1f;
        private Vector2 _rotationRandomRange = new Vector2(0f, 360f);
        private Vector2 _scaleRandomRange = new Vector2(1f, 1f);
        private float _angleLimit = 45f;

        [SerializeField] private bool _raycastTraceEnabled = false;
        [SerializeField] private bool _raycastTraceShowAll = false;
        [SerializeField] private float _raycastTraceBoundsStepSec = 0.1f;
        [SerializeField] private float _raycastTracePolyStepSec = 0.002f;
        [SerializeField] private Color _raycastTraceBoundsColor = new Color(0f, 0.85f, 1f, 0.5f);
        [SerializeField] private Color _raycastTracePolyColor = new Color(1f, 0.9f, 0f, 0.3f);
        [SerializeField] private Color _raycastTraceHitColor = new Color(0.1f, 1f, 0.1f, 0.45f);
        [SerializeField] private Color _raycastTraceCurrentColor = new Color(1f, 0.2f, 0.2f, 0.65f);
        private List<RaycastTraceStep> _raycastTraceSteps;
        private int _raycastTraceIndex;
        private double _raycastTraceStartTime;

        private bool _alignNormal = false;
        public bool AlignTerrainNormal
        {
            get => _alignNormal;
            set => _alignNormal = value;
        }
        private bool _debug = false;
        [MenuItem("Playwith/TA/Foliage Day", false)]
        public static void ShowWindow()
        {
            var window = EditorWindow.GetWindow(typeof(FoliageDayWindow), false, "Foliage Day") as FoliageDayWindow;
            window.minSize = new Vector2(420, 650);
            window.Initialize();
            window.Show();
        }

        public void Initialize()
        {
            if (null == _foliageManager)
            {
                _foliageManager = FoliageManager.FindOrCreate();
            }
            SetFoliageManager(_foliageManager);
            InitGUI();
        }

        private void SetFoliageManager(FoliageManager foliageManager = null)
        {
            _editMode = eEditMode.Select;
            if (null == foliageManager)
                return;

            _foliageManager = foliageManager;
            _foliageManager.Initialize();

            if (!_foliageManager.CheckGeometryNodeValid())
                _foliageManager.InitializeNodes();

            _terrainRoot = _foliageManager.TerrainRoot;
            _paletteAsset = _foliageManager.PaletteAsset;

            _brush = new FoliageBrush(_foliageManager);
            _brush.Radius = _radius;
            _brush.OverwrapRatio = _overwrapRatio;
            _brush.EraseDensity = _eraseDensity;

            SetEditMode(eEditMode.Select);
            ReloadPreview();
        }

        private void InitGUI()
        {
            _guiSkin = AssetDatabase.LoadAssetAtPath<GUISkin>("Assets/Scripts/FoliageEngine/Editor/GUISkin.guiskin");

            var fontStyle = new GUIStyle();
            fontStyle.fontSize = 15;

            _guiStyles = new GUIStyle[1];
            _guiStyles[0] = fontStyle;

            _guidGridContents = new GUIContent[]
            {
                new GUIContent("선택", "심기 비활성"),
                new GUIContent("심기(F2)", "풀을 심습니다."),
                new GUIContent("뽑기(F3)", "풀을 제거합니다."),
            };

            _guiButtonContents = new GUIContent[]
            {
                new GUIContent("변환", "브러쉬로 배치한 풀을 씬에 적용합니다."),
                new GUIContent("수집", "기존에 배치된 풀들을 수정 가능하도록 수집합니다.")
            };

            _lastSelectedTool = Tools.current;
            SceneView.duringSceneGui += OnSceneGUI;

            _foliageManager = FoliageManager.FindOrCreate();
            _foliageManager.Initialize();
        }

        private void AddEmptyPalette()
        {
            var emptyInfo = new FoliagePaletteSlotData()
            {
                slotIdx = _previewPlantList.Count
            };

            var preview = new FoliageViewEditor(emptyInfo, OnToggleFoliageSlot, OnChangePrefab, OnRemoveFoliageSlot, _guiSkin);
            _previewPlantList.Add(preview);
            Repaint();
        }

        private void ReloadPreview()
        {
            for (int i = 0; i < _previewPlantList.Count; ++i)
            {
                _previewPlantList[i].ReleasePreview();
            }
            _previewPlantList.Clear();

            if (null == _foliageManager.PaletteAsset)
                return;

            var infoList = _foliageManager.PaletteAsset;
            foreach (var info in infoList.paletteSlots)
            {
                var preview = new FoliageViewEditor(info, OnToggleFoliageSlot, OnChangePrefab, OnRemoveFoliageSlot, _guiSkin);
                _previewPlantList.Add(preview);
            }
            Repaint();
        }

        private void OnToggleFoliageSlot(bool value, FoliagePaletteSlotData info)
        {
            if (null == info.prefab)
                return;

            selectedPaletteSlotIdx = info.SlotIdx;
            isDirty = true;
            Repaint();
        }

        private void OnChangePrefab(FoliagePaletteSlotData info)
        {
            _foliageManager?.SetPaletteSlot(info.SlotIdx, info);
        }

        private void OnRemoveFoliageSlot(FoliagePaletteSlotData info)
        {
            if (selectedPaletteSlotIdx == info.SlotIdx)
            {
                selectedPaletteSlotIdx = 0;
            }

            EditorApplication.delayCall += () =>
            {
                RemovePreview(info.SlotIdx);
            };
            isDirty = true;
        }

        private void RemovePreview(int slotIdx)
        {
            var preview = _previewPlantList.FirstOrDefault(p => p.SlotIdx == slotIdx);
            if (preview != null)
            {
                preview.ReleasePreview();
                _previewPlantList.Remove(preview);
            }
            _foliageManager.RemovePaletteSlot(slotIdx);
            selectedPaletteSlotIdx = Math.Max(0, _previewPlantList.Count - 1);
        }

        private bool isDirty = false;
        private int selectedPaletteSlotIdx = 0;
        private string _sceneMessage = "";
        private double _sceneMessageUntil = 0f;

        private void ShowSceneViewMessage(string message, float duration = 2f)
        {
            _sceneMessage = message;
            _sceneMessageUntil = EditorApplication.timeSinceStartup + duration;
        }

        public void SetSelectedPaletteSlotIdx(int idx)
        {
            if (idx < 0 || idx >= _paletteAsset.PalleteCount)
                return;

            selectedPaletteSlotIdx = idx;
            isDirty = true;

            scrollPos = new Vector2(idx * FoliageViewEditor.PREVIEW_WIDTH, 0);

            var slot = _paletteAsset.GetSlot(idx);
            if (null == slot || null == slot.prefab)
                return;

            ShowSceneViewMessage($"#{idx}. {slot.prefab.name}", 1f);
        }

        private void OnEnable()
        {
            EditorApplication.delayCall += RepainAllNextFrame;
            ShowSceneViewMessage(_sceneMessage, 3f);
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= RepainAllNextFrame;
        }

        private void OnDestroy()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private UnityEditor.Tool _lastSelectedTool;

        private void SetEditMode(eEditMode mode)
        {
            if (mode == _editMode)
                return;

            if (null == _foliageManager)
            {
                Debug.LogWarning("FoliageManager is not set");
                _editMode = eEditMode.Select;
                return;
            }

            _editMode = mode;
            if (_editMode != eEditMode.Select)
            {
                Tools.current = UnityEditor.Tool.View;
                _brush?.RefreshFoliages(_foliageManager.GeometryNode);
            }
            else
            {
                Tools.current = _lastSelectedTool;
            }

            ShowSceneViewMessage($"{_editMode.ToString()}", 1f);
        }

        private void DrawModePart()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                FoliageEngine.ShowCount = GUILayout.Toggle(FoliageEngine.ShowCount, " 렌더링 개수 표시", _guiSkin.toggle);
                EditorGUILayout.LabelField($"{FoliageEngine.VisCount.ToString("N0")} / {FoliageEngine.AllCount.ToString("N0")}", _guiSkin.label);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("저장", GUILayout.Width(100)))
                {
                    if (EditorUtility.DisplayDialog("저장", "변경된 내용을 저장하시겠습니까?", "저장", "취소"))
                    {
                        _foliageManager?.Save();
                    }
                }
                if (GUILayout.Button("다시 로드", GUILayout.Width(100)))
                {
                    _foliageManager?.Initialize(true);
                    SetFoliageManager(_foliageManager);
                }
            }
        }

        private void DrawFunctionButton()
        {
            int selection = (int)_editMode;
            selection = GUILayout.SelectionGrid(selection, _guidGridContents, 3, _guiSkin.button);
            SetEditMode((eEditMode)selection);
            EditorGUILayout.Separator();

            if (GUILayout.Button(_guiButtonContents[0], _guiSkin.button))
            {
                if (EditorUtility.DisplayDialog("변환", "배치된 식생을 오브젝트로 변환하시겠어요?", "네", "아녀"))
                {
                    _foliageManager.GenerateGameObject();
                }
            }

            if (GUILayout.Button(_guiButtonContents[1], _guiSkin.button))
            {
                OnRetrieveSceneFoliages();
            }
        }

        private void OnRetrieveSceneFoliages()
        {
            FoliageRetrievePopup.Show((isOk, objects) =>
             {
                 if (isOk)
                 {
                     RetrieveSceneFoliages(objects);
                 }
             });
        }

        private void RetrieveSceneFoliages(List<Transform> rootObjects)
        {
            if (null == _foliageManager)
                return;

            if (null == _foliageManager.PaletteAsset)
            {
                _foliageManager.CreatePaletteAsset();
            }

            Dictionary<GameObject, List<Transform>> prefabFoliagesDict = new Dictionary<GameObject, List<Transform>>();
            Dictionary<GameObject, string> prefabPathDict = new Dictionary<GameObject, string>();

            foreach (var root in rootObjects)
            {
                if (null == root)
                    continue;

                for (int i = 0; i < root.childCount; i++)
                {
                    var child = root.GetChild(i);
                    if (null == child)
                        continue;

                    var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child);
                    if (string.IsNullOrEmpty(prefabPath))
                        continue;

                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (null == prefab)
                        continue;

                    if (!prefabFoliagesDict.ContainsKey(prefab))
                    {
                        prefabFoliagesDict[prefab] = new List<Transform> { child };
                        prefabPathDict[prefab] = prefabPath;
                    }
                    else
                    {
                        prefabFoliagesDict[prefab].Add(child);
                    }
                }
            }

            foreach (var pair in prefabFoliagesDict)
            {
                var prefab = pair.Key;
                var foliagesObjects = pair.Value;
                int paletteSlotIdx = -1;
                FoliagePaletteSlotData paletteSlot = null;
                if (!_foliageManager.CheckPaletteSlotExists(prefab))
                {
                    var prefabPath = prefabPathDict[prefab];
                    paletteSlotIdx = _foliageManager.GetPaletteCount();
                    paletteSlot = new FoliagePaletteSlotRuntime(paletteSlotIdx, prefab, prefabPath, Vector3.zero, Quaternion.identity, Vector3.one);
                    _foliageManager.AddPaletteSlot(paletteSlot);
                }
                else
                {
                    paletteSlot = _foliageManager.GetPalette(prefab);
                    paletteSlotIdx = paletteSlot.SlotIdx;
                }

                if (null == paletteSlot || paletteSlotIdx == -1)
                {
                    Debug.LogWarning($"invalid palette slot {prefab.name}");
                    continue;
                }

                for (int i = 0; i < foliagesObjects.Count; i++)
                {
                    var foliageObject = foliagesObjects[i];
                    _foliageManager.AddFoliage(paletteSlotIdx, new FoliageInstance_Editor(paletteSlot, i, foliageObject.position, foliageObject.rotation, foliageObject.localScale));
                }
            }
            _foliageManager.Update();
            ReloadPreview();
        }

        private void DrawLayerPart()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(" 터레인 메시 루트", _guiSkin.label);
                _terrainRoot = (Transform)EditorGUILayout.ObjectField(_terrainRoot, typeof(Transform), true);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(" 팔레트 애셋", _guiSkin.label);
                _paletteAsset = (FoliagePaletteAsset)EditorGUILayout.ObjectField(_paletteAsset, typeof(FoliagePaletteAsset), true);
            }

            if (_terrainRoot != _foliageManager.TerrainRoot)
            {
                _foliageManager.SetTerrainRoot(_terrainRoot);
                _foliageManager.Initialize(true);
            }
            EditorGUILayout.Separator();
        }

        private void DrawBrushPart()
        {
            EditorGUILayout.Space(3);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("반지름");
                var radius = EditorGUILayout.Slider(_radius, FoliageBrush.MinRadius, FoliageBrush.MaxRadius);
                if (radius != _radius)
                {
                    _radius = radius;
                    _brush.Radius = _radius;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("간격 비율");
                var overwrapRatio = EditorGUILayout.Slider(_overwrapRatio, FoliageBrush.MinOverwrapRatio, FoliageBrush.MaxOverwrapRatio);
                if (overwrapRatio != _overwrapRatio)
                {
                    _overwrapRatio = overwrapRatio;
                    _brush.OverwrapRatio = _overwrapRatio;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("섞기 강도");
                var mixture = EditorGUILayout.Slider(_mixtureRate, 0f, 1f);
                if (mixture != _mixtureRate)
                {
                    _mixtureRate = mixture;
                    _brush.MixtureRate = _mixtureRate;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("지우기 강도");
                var eraseDensity = EditorGUILayout.Slider(_eraseDensity, 0.1f, 1f);
                if (eraseDensity != _eraseDensity)
                {
                    _eraseDensity = eraseDensity;
                    _brush.EraseDensity = _eraseDensity;
                }
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("랜덤 스케일 범위", GUILayout.Width(120));
                    Vector2 scaleRandomRange = _scaleRandomRange;
                    EditorGUILayout.MinMaxSlider(ref scaleRandomRange.x, ref scaleRandomRange.y, 0.1f, 2f, GUILayout.Width(195));
                    scaleRandomRange.x = EditorGUILayout.FloatField(scaleRandomRange.x, GUILayout.Width(32));
                    EditorGUILayout.LabelField("~", GUILayout.Width(10));
                    scaleRandomRange.y = EditorGUILayout.FloatField(scaleRandomRange.y, GUILayout.Width(30));
                    if (scaleRandomRange != _scaleRandomRange)
                    {
                        _scaleRandomRange = scaleRandomRange;
                        _brush.ScaleRandomRange = new Vector2(scaleRandomRange.x, scaleRandomRange.y);
                    }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("랜덤 회전 범위", GUILayout.Width(120));
                    Vector2 rotationRandomRange = _rotationRandomRange;
                    EditorGUILayout.MinMaxSlider(ref rotationRandomRange.x, ref rotationRandomRange.y, 0f, 360f);
                    rotationRandomRange.x = EditorGUILayout.FloatField(rotationRandomRange.x, GUILayout.Width(32));
                    EditorGUILayout.LabelField("~", GUILayout.Width(10));
                    rotationRandomRange.y = EditorGUILayout.FloatField(rotationRandomRange.y, GUILayout.Width(30));
                    if (rotationRandomRange != _rotationRandomRange)
                    {
                        _rotationRandomRange = rotationRandomRange;
                        _brush.RotationRandomRange = _rotationRandomRange;
                    }
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("경사각 제한", GUILayout.Width(120));
                    var angleLimit = EditorGUILayout.Slider(_angleLimit, 0f, 90);
                    if (angleLimit != _angleLimit)
                    {
                        _angleLimit = angleLimit;
                        var euler = new Vector3(0f, _angleLimit, 0f);
                        var dot = Quaternion.Euler(euler) * Vector3.up;
                        _brush.AngleLimit = Vector3.Dot(dot, Vector3.up);
                    }
                }
            }
            EditorGUILayout.Space(4);

            titleContent.text = _editMode == eEditMode.Select ? "선택 모드" :
                _editMode == eEditMode.Paint ? "그리기 모드" : "지우기 모드";
        }


        private Vector2 scrollPos = Vector2.zero;

        private void DrawPrefabView()
        {
            if (null == _foliageManager || null == _foliageManager.PaletteAsset)
                return;

            if (_previewPlantList.Count > 0)
            {
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos, false, false, GUILayout.Height(230));
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int i = 0; i < _previewPlantList.Count; i++)
                    {
                        var view = _previewPlantList[i];
                        var rect = EditorGUILayout.GetControlRect();
                        view.OnDraw(new Rect(rect.x - (i * 50), rect.y, 150, 130), selectedPaletteSlotIdx);
                    }
                }
                EditorGUILayout.EndScrollView();
                GUI.color = Color.white;
            }
            EditorGUILayout.Separator();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.color = Color.green;
                if (GUILayout.Button("식생 추가", GUILayout.Height(30)))
                {
                    AddEmptyPalette();
                }
                GUI.color = Color.white;
            }
            EditorGUILayout.Space(4);
        }

        private void DrawFuctionButton()
        {

        }

        private void DrawWarningMessage(string message)
        {
            EditorGUILayout.HelpBox(message, MessageType.Warning);
        }

        private void DrawErrorMessage(string message)
        {
            EditorGUILayout.HelpBox(message, MessageType.Error);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("모드", EditorStyles.boldLabel);
                DrawModePart();
                EditorGUILayout.Space(4);
                DrawLayerPart();
            }

            if (null == _terrainRoot)
            {
                DrawErrorMessage("터레인 메시 루트를 선택해 주세요.");
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                DrawFunctionButton();
            }

            if (null != _foliageManager && null == _paletteAsset)
            {
                _foliageManager.SetPaletteAsset(FoliageManager.CreateFoliagePaletteAsset());
                _paletteAsset = _foliageManager.PaletteAsset;
            }

            if (null != _foliageManager && null != _paletteAsset && _previewPlantList.Count == 0)
            {
                DrawWarningMessage("팔레트가 비어있습니다. 식생을 추가해 주세요.");
            }

            if (null != _foliageManager && null != _paletteAsset)
            {
                EditorGUILayout.Space(4);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("브러시", EditorStyles.boldLabel);
                    DrawBrushPart();
                }

                EditorGUILayout.Space(4);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("팔레트", EditorStyles.boldLabel);
                    DrawPrefabView();
                }

                DrawFuctionButton();
            }
            EditorGUILayout.Space(6);
        }


        private void Update()
        {
            if (isDirty)
            {
                ReloadPreview();
                isDirty = false;
            }
        }

        void OnSceneGUI(SceneView sceneView)
        {
            if (null == _foliageManager)
                return;

            OnUpdateEvent();
            OnDrawMessage(sceneView);
            OnDrawBrushGizmo();
            DrawRaycastTraceGizmo(sceneView);
        }

        private void OnDrawMessage(SceneView sceneView)
        {
            if (string.IsNullOrEmpty(_sceneMessage))
                return;

            if (EditorApplication.timeSinceStartup > _sceneMessageUntil)
                return;

            Handles.BeginGUI();

            float width = 300f;
            float height = 30f;
            float x = (sceneView.position.width - width) * 0.5f;
            float y = sceneView.position.height * 0.2f;

            var rect = new Rect(x, y, width, height);

            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16
            };

            GUI.Label(rect, _sceneMessage, style);

            Handles.EndGUI();
        }

        private void OnDrawBrushGizmo()
        {
            if (_editMode == eEditMode.Select)
                return;

            if (null == _brush)
                return;

            var currentPaletteSlot = GetCurrentPaletteSlot();
            if (null == currentPaletteSlot)
                return;

            if (_editMode == eEditMode.Paint)
            {
                Handles.color = new Color(0f, 0f, 1f, 0.3f);
                Handles.DrawSolidDisc(
                    _brush.MovePoint,
                    _brush.MoveNormal,
                    _brush.Radius
                );
                Handles.color = new Color(1f, 1f, 1f, 0.3f);
                Handles.DrawSolidDisc(
                    _brush.MovePoint,
                    _brush.MoveNormal,
                    currentPaletteSlot.Radius * _brush.OverwrapRatio
                );
                Handles.color = Color.white;
                Handles.ArrowHandleCap(
                    0,
                    _brush.MovePoint,
                    Quaternion.LookRotation(_brush.MoveNormal),
                    _brush.Radius * 0.5f,
                    EventType.Repaint
                );
            }
            else if (_editMode == eEditMode.Erase)
            {
                Handles.color = new Color(1f, 1f, 1f, 0.3f);
                Handles.DrawSolidDisc(
                    _brush.MovePoint,
                    _brush.MoveNormal,
                    _brush.Radius
                );
                Handles.color = new Color(1f, 0f, 0f, 0.4f * _brush.EraseDensity);
                Handles.DrawSolidDisc(
                    _brush.MovePoint,
                    _brush.MoveNormal,
                    _brush.Radius
                );
                Handles.color = Color.white;
                Handles.ArrowHandleCap(
                    0,
                    _brush.MovePoint,
                    Quaternion.LookRotation(_brush.MoveNormal),
                    _brush.Radius * 0.5f,
                    EventType.Repaint
                );
            }
        }

        public void PlayRaycastTrace(List<RaycastTraceStep> traceSteps, bool showAllPrevious = true)
        {
            _raycastTraceSteps = traceSteps;
            _raycastTraceBoundsStepSec = Mathf.Max(0.001f, _raycastTraceBoundsStepSec);
            _raycastTracePolyStepSec = Mathf.Max(0.001f, _raycastTracePolyStepSec);
            _raycastTraceShowAll = showAllPrevious;
            _raycastTraceIndex = 0;
            _raycastTraceStartTime = EditorApplication.timeSinceStartup;
            _raycastTraceEnabled = _raycastTraceSteps != null && _raycastTraceSteps.Count > 0;
            _drwEndTime = (float)(EditorApplication.timeSinceStartup + 60f);
            SceneView.RepaintAll();
        }

        public void StopRaycastTrace()
        {
            _raycastTraceEnabled = false;
            _raycastTraceSteps = null;
            _raycastTraceIndex = 0;
            SceneView.RepaintAll();
        }

        float _drwEndTime = 0f;
        private void DrawRaycastTraceGizmo(SceneView sceneView)
        {
            if (!_debug)
                return;

            if (!_raycastTraceEnabled || _raycastTraceSteps == null || _raycastTraceSteps.Count == 0)
                return;

            int maxIndex = _raycastTraceSteps.Count - 1;
            float time = (float)(EditorApplication.timeSinceStartup - _raycastTraceStartTime);
            int targetIndex = GetRaycastTraceIndexByTime(time);

            _raycastTraceIndex = Mathf.Clamp(targetIndex, 0, maxIndex);
            if (_raycastTraceIndex < maxIndex)
                sceneView.Repaint();

            int startIndex = _raycastTraceShowAll ? 0 : _raycastTraceIndex;
            UnityEngine.Rendering.CompareFunction prevZTest = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            for (int i = startIndex; i <= _raycastTraceIndex; i++)
            {
                var step = _raycastTraceSteps[i];
                if (step.Shape == RaycastTraceShape.Ray)
                {
                    Handles.color = Color.yellow;
                    Handles.DrawLine(step.Ray.origin, step.Ray.origin + step.Ray.direction * 100f);
                }

                Color color = step.Hit ? _raycastTraceHitColor : _raycastTracePolyColor;
                if (step.Shape == RaycastTraceShape.Bounds)
                    color = _raycastTraceBoundsColor;
                if (i == _raycastTraceIndex)
                    color = (_raycastTraceIndex == maxIndex) ? color : _raycastTraceCurrentColor;

                if (step.Shape == RaycastTraceShape.Bounds)
                {
                    Handles.color = color;
                    DrawBoundsWithLines(step.Bounds, 3f);
                }
                else if (step.TryGetPolygon(out var polygon))
                {
                    Handles.color = color;
                    Handles.DrawAAConvexPolygon(polygon);
                    Handles.color = new Color(color.r, color.g, color.b, 1f);
                    for (int p = 0; p < polygon.Length; p++)
                    {
                        var a = polygon[p];
                        var b = polygon[(p + 1) % polygon.Length];
                        Handles.DrawLine(a, b, 2f);
                    }
                }
            }

            Handles.zTest = prevZTest;
            if ((float)(EditorApplication.timeSinceStartup) > _drwEndTime)
                StopRaycastTrace();
        }

        private static void DrawBoundsWithLines(Bounds bounds, float width = 2f)
        {
            var min = bounds.min;
            var max = bounds.max;

            var p0 = new Vector3(min.x, min.y, min.z);
            var p1 = new Vector3(max.x, min.y, min.z);
            var p2 = new Vector3(max.x, min.y, max.z);
            var p3 = new Vector3(min.x, min.y, max.z);

            var p4 = new Vector3(min.x, max.y, min.z);
            var p5 = new Vector3(max.x, max.y, min.z);
            var p6 = new Vector3(max.x, max.y, max.z);
            var p7 = new Vector3(min.x, max.y, max.z);

            Handles.DrawLine(p0, p1, width);
            Handles.DrawLine(p1, p2, width);
            Handles.DrawLine(p2, p3, width);
            Handles.DrawLine(p3, p0, width);

            Handles.DrawLine(p4, p5, width);
            Handles.DrawLine(p5, p6, width);
            Handles.DrawLine(p6, p7, width);
            Handles.DrawLine(p7, p4, width);

            Handles.DrawLine(p0, p4, width);
            Handles.DrawLine(p1, p5, width);
            Handles.DrawLine(p2, p6, width);
            Handles.DrawLine(p3, p7, width);
        }

        private int GetRaycastTraceIndexByTime(float timeSec)
        {
            if (_raycastTraceSteps == null || _raycastTraceSteps.Count == 0)
                return 0;

            float accumulated = 0f;
            for (int i = 0; i < _raycastTraceSteps.Count; i++)
            {
                var step = _raycastTraceSteps[i];
                var stepSec = step.Shape == RaycastTraceShape.Bounds ? _raycastTraceBoundsStepSec : _raycastTracePolyStepSec;
                accumulated += Mathf.Max(0.01f, stepSec);
                if (timeSec < accumulated)
                    return i;
            }
            return _raycastTraceSteps.Count - 1;
        }

        public FoliagePaletteSlotData GetCurrentPaletteSlot()
        {
            return _paletteAsset.GetSlot(selectedPaletteSlotIdx);
        }

        private bool ProcessKeyEvent(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.Escape:
                    SetEditMode(eEditMode.Select);
                    return true;
                case KeyCode.F2:
                    SetEditMode(eEditMode.Paint);
                    return true;
                case KeyCode.F3:
                    SetEditMode(eEditMode.Erase);
                    return true;
                case KeyCode.Alpha1:
                    SetSelectedPaletteSlotIdx(0);
                    return true;
                case KeyCode.Alpha2:
                    SetSelectedPaletteSlotIdx(1);
                    return true;
                case KeyCode.Alpha3:
                    SetSelectedPaletteSlotIdx(2);
                    return true;
                case KeyCode.Alpha4:
                    SetSelectedPaletteSlotIdx(3);
                    return true;
                case KeyCode.Alpha5:
                    SetSelectedPaletteSlotIdx(4);
                    return true;
                case KeyCode.Alpha6:
                    SetSelectedPaletteSlotIdx(5);
                    return true;
                case KeyCode.Alpha7:
                    SetSelectedPaletteSlotIdx(6);
                    return true;
                case KeyCode.Alpha8:
                    SetSelectedPaletteSlotIdx(7);
                    return true;
                case KeyCode.Alpha9:
                    SetSelectedPaletteSlotIdx(8);
                    return true;
                default:
                    return false;
            }
        }
        bool useDebug = false;
        private void OnUpdateEvent()
        {
            if (EditorWindow.mouseOverWindow is SceneView)
            {
                if (Event.current.type == EventType.KeyDown)
                {
                    if (ProcessKeyEvent(Event.current.keyCode))
                    {
                        Repaint();
                        Event.current.Use();
                    }
                }

                if (_editMode == eEditMode.Select)
                    return;

                if (Event.current.alt)
                    return;

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                    OnPreUpdatePointer(ray);

                    if (_editMode == eEditMode.Paint)
                    {
                        if (_paletteAsset.PalleteCount == 0)
                        {
                            EditorUtility.DisplayDialog("저기요!", "팔레트가 비어있어요.", "OK");
                            return;
                        }

                        if (_brush.Paint(GetCurrentPaletteSlot(), useDebug))
                        {
                            PlayRaycastTrace(_brush.TraceSteps, _raycastTraceShowAll);
                            UpdateBrushCache();
                        }
                    }
                    else if (_editMode == eEditMode.Erase)
                    {
                        if (_brush.Erase())
                        {
                            UpdateBrushCache();
                        }
                    }

                    OnPostUpdatePointer();

                    Event.current.Use();
                }
                else if (Event.current.type == EventType.MouseDrag && Event.current.button == 0)
                {
                    var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                    OnPreUpdatePointer(ray);
                    switch (_editMode)
                    {
                        case eEditMode.Paint:
                            if (_brush.UpdateMouseDragPlant(ray, GetCurrentPaletteSlot()))
                            {
                                UpdateBrushCache();
                            }
                            break;
                        case eEditMode.Erase:
                            if (_brush.UpdateMouseDragErase(ray))
                            {
                                UpdateBrushCache();
                            }
                            break;
                    }
                    OnPostUpdatePointer();
                    Event.current.Use();
                    SceneView.RepaintAll();
                }
                else if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
                {
                    _brush.OnPress = false;
                    var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                    OnPreUpdatePointer(ray);
                    OnPostUpdatePointer();
                    Repaint();
                    Event.current.Use();
                    SceneView.RepaintAll();
                }
                else if (Event.current.type == EventType.MouseMove)
                {
                    var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                    OnPreUpdatePointer(ray);
                    _brush.UpdateMosueMove(ray);
                    OnPostUpdatePointer();
                    SceneView.RepaintAll();
                }
                else if (Event.current.isScrollWheel)
                {
                    if (Event.current.shift)
                    {
                        var newRadius = _radius + Event.current.delta.x * 0.1f;
                        _radius = Mathf.Max(FoliageBrush.MinRadius, Mathf.Min(FoliageBrush.MaxRadius, newRadius));
                        _brush.Radius = _radius;

                        ShowSceneViewMessage($"반지름: {_radius}", 1f);

                        Event.current.Use();
                        Repaint();
                    }
                    else if (Event.current.control)
                    {
                        if (_editMode == eEditMode.Paint)
                        {
                            var newOverwrapRatio = _overwrapRatio + Event.current.delta.y * 0.02f;
                            _overwrapRatio = Mathf.Max(FoliageBrush.MinOverwrapRatio, Mathf.Min(FoliageBrush.MaxOverwrapRatio, newOverwrapRatio));
                            _brush.OverwrapRatio = _overwrapRatio;
                            Event.current.Use();

                            ShowSceneViewMessage($"밀도: {_overwrapRatio}", 1f);

                            Repaint();
                        }
                        else if (_editMode == eEditMode.Erase)
                        {
                            var newEraseDensity = _eraseDensity + Event.current.delta.y * 0.02f;
                            _eraseDensity = Mathf.Max(FoliageBrush.MinEraseDensity, Mathf.Min(FoliageBrush.MaxEraseDensity, newEraseDensity));
                            _brush.EraseDensity = _eraseDensity;
                            Event.current.Use();

                            ShowSceneViewMessage($"밀도: {_eraseDensity}", 1f);

                            Repaint();
                        }
                    }
                }
            }
        }

        private void UpdateBrushCache()
        {
            bool hasChanged = false;
            if (_brush.CachingData.Count > 0)
            {
                _foliageManager.AddFoliages(selectedPaletteSlotIdx, _brush.CachingData);
                hasChanged = true;
            }

            if (_brush.RemoveCandiates.Count > 0)
            {
                _foliageManager.RemoveEachFoliages(_brush.RemoveCandiates);
                hasChanged = true;
            }

            if (hasChanged)
                _foliageManager.Update();
        }

        private void RepainAllNextFrame()
        {
            SceneView.RepaintAll();
        }

        private bool OnPreUpdatePointer(Ray ray)
        {
            return _brush.UpdateMosueMove(ray);
        }

        private void OnPostUpdatePointer()
        {
            if (null == _foliageManager)
                return;
        }
    }
}