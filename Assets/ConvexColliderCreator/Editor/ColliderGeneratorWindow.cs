// Copyright (c) 2018 Justin Couch / JustInvoke
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEditor.SceneManagement;
using UnityEditorInternal;

namespace ConvexColliderCreator
{
    //Class for the collider editor window
    public class ColliderGeneratorWindow : EditorWindow
    {
        [MenuItem("Window/Convex Collider Creator &c")]
        public static void ShowWindow()
        {
            //Show the editor window
            GetWindow<ColliderGeneratorWindow>("Convex Collider Creator");
        }

        public static ColliderGeneratorWindow window;

        //Check if the editor window is open
        public static bool WindowIsOpen()
        {
            return window != null;
        }

        public static bool reloadOnCompile = true;//Whether to close and reopen the editor after code compilation
        //This is useful because recompiling code can interfere with undo states, however the window will also un-dock itself

        [UnityEditor.Callbacks.DidReloadScripts]
        //Close and reopen window on compile
        static void ReloadOnCompile()
        {
            if (reloadOnCompile)
            {
                Reload();
            }
        }

        void ReloadOnSceneChange(Scene scene, OpenSceneMode mode)
        {
            if (mode == OpenSceneMode.Single)
            {
                Reload();
            }
        }

        //Closes and reopens the window
        static void Reload()
        {
            if (WindowIsOpen())
            {
                window.Close();
                ShowWindow();
            }
        }

        bool livePreview = true;
        bool previewAllColliders = true;
        bool liveEdit = false;//This was for updating the actual collider components continuously but has been disabled because it caused issues with undoing
        float sceneUpdateTime = 0.2f;
        double lastUpdateTime = 0.0f;
        enum MeshPreviewMode { None, RealMesh, BoxApproximation } //Box approximation uses gizmos to draw a rough approximation, real mesh draws the actual mesh
        MeshPreviewMode previewMode = MeshPreviewMode.RealMesh;
        List<Mesh> colMeshes = new List<Mesh>(new Mesh[1] { null });
        Material liveMat;//Material used for drawing preview meshes
        Material liveWireMat;//Material for drawing wireframes of preview meshes
        string liveMatShader = "Convex Collider Creator/Mesh Preview";
        string liveMatWireShader = "Convex Collider Creator/Mesh Preview Wire";

        GameObject target;
        GameObject targetPrev;
        bool showTarget = true;
        bool targetIsSelection = true;
        bool nullClearTarget = false;
        public bool useColliderGroup = true;
        bool dontDeleteColliderGroup = false;
        bool settingTargetFromGroup = false;
        int targetGroupColIndex = -1;
        ColliderGroup colGroup;//Current collider group being edited
        ColliderGroupTargetInfo colGroupInfoTemp = new ColliderGroupTargetInfo();
        int colIndex = 0;//Index of collider being edited in group
        int colIndexPrev = -1;
        List<MeshCollider> meshCols = new List<MeshCollider>();
        GeneratorProps genProps = new GeneratorProps(Vector3.one, 0.1f);//Generator properties that are currently being modified
        GeneratorProps genPropsTemp = new GeneratorProps(Vector3.one, 0.1f);//Placeholder generator properties, used for creating new colliders

        //This limits the index of the collider being modified after certain events such as deletion
        public void LimitIndex(int max)
        {
            colIndex = Mathf.Clamp(colIndex, 0, Mathf.Max(0, max - 1));
            UpdateColliderFromIndex(max);
        }

        //Variables for showing and hiding foldouts in the window
        bool showOptions = false;
        bool showSaveOptions = false;
        bool showMirroringAndFlipping = true;
        bool showPositionHandles = true;
        bool showRadiusHandles = true;
        bool showRadiusGizmos = true;
        bool showResetButtons = false;
        bool showCorners = true;
        bool showCornerPositions = true;
        bool showCornerRadii = true;
        bool showCornerRadiusOffsets = false;
        bool showDetail = true;
        bool showHooks = true;
        bool showHookHandles = true;
        bool showHookPositionHandles = true;
        bool showHookRotationHandles = true;
        bool showHookRadiusHandles = true;
        bool showHookStrengthHandles = true;
        bool showHookNames = true;

        bool allowDeletion = false;
        bool allowMirroring = true;
        bool allowFlipping = true;

        bool errorOccurred = false;

        enum CornerHandleEditMode { Radii, Offsets }
        CornerHandleEditMode cornerHandleMode = CornerHandleEditMode.Radii;//Whether the radius handles are for adjusting the actual corner radii or the radius offsets

        //Animation bools for animating foldouts in the window
        AnimBool cEditFadeBoxP;//cEdit = "corner edit", Fade = "fade animation", BoxP = "box position"
        AnimBool cEditFadeSideP;//SideP = "box side position"
        AnimBool cEditFadeIndP;//IndP = "individual position"
        GeneratorProps.CornerPositionEditMode cPosModePrev = GeneratorProps.CornerPositionEditMode.BoxCenter;

        AnimBool cEditFadeUniR0;//UniR = "all uniform radii", R0 = "radius editing"
        AnimBool cEditFadeIndR0;//IndR = "individual uniform radii"
        AnimBool cEditFadeAdvR0;//AdvR = "advanced radii (individual components)"
        float maxCornerRadius = 1.0f;

        AnimBool cEditFadeUniR1;//R1 = "radius offset editing"
        AnimBool cEditFadeIndR1;
        AnimBool cEditFadeAdvR1;
        float maxCornerOffset = 1.0f;

        AnimBool cEditDetAll;//DetAll = "all detail"
        AnimBool cEditDetInd;//DetInd = "individual detail"

        SerializedObject sWin;//Serialized object reference for the window
        SerializedProperty sHooks;//Serialized property reference for the active hook list
        public DeformHook[] hooks = new DeformHook[0];//Active hooks being modified
        public ReorderableList hookList;//Special reorderable hook list for the window
        Vector2 scrollView;
        float guiWidthScale = 1.0f;//Scales the width of GUI elements in the window
        bool drawMaxIndexLabel = false;//Whether to draw the max index label, this is to avoid layout mismatch errors bewtween the layout and repaint events

        //Save all editor window preferences (called when window or Unity editor is closed)
        private void SaveSettings()
        {
            EditorPrefs.SetBool("CCC Reload Compile", reloadOnCompile);
            EditorPrefs.SetFloat("CCC GUI Width", guiWidthScale);
            EditorPrefs.SetBool("CCC Show Options", showOptions);
            EditorPrefs.SetBool("CCC Show Save Options", showSaveOptions);
            EditorPrefs.SetBool("CCC Show Target", showTarget);
            EditorPrefs.SetBool("CCC Show Mirror", showMirroringAndFlipping);
            EditorPrefs.SetBool("CCC Show Corners", showCorners);
            EditorPrefs.SetBool("CCC Show Corner Positions", showCornerPositions);
            EditorPrefs.SetBool("CCC Show Corner Radii", showCornerRadii);
            EditorPrefs.SetBool("CCC Show Corner Radius Offsets", showCornerRadiusOffsets);
            EditorPrefs.SetBool("CCC Show Detail", showDetail);
            EditorPrefs.SetBool("CCC Show Hooks", showHooks);
            EditorPrefs.SetBool("CCC Show Reset Buttons", showResetButtons);

            EditorPrefs.SetBool("CCC Live Preview", livePreview);
            EditorPrefs.SetBool("CCC Preview All", previewAllColliders);
            EditorPrefs.SetFloat("CCC Preview Time", sceneUpdateTime);
            EditorPrefs.SetInt("CCC Preview Mode", (int)previewMode);

            EditorPrefs.SetBool("CCC Target Select", targetIsSelection);
            EditorPrefs.SetBool("CCC Null Select", nullClearTarget);

            EditorPrefs.SetBool("CCC Allow Delete", allowDeletion);
            EditorPrefs.SetBool("CCC Allow Mirror", allowMirroring);
            EditorPrefs.SetBool("CCC Allow Flip", allowFlipping);

            EditorPrefs.SetBool("CCC Position Handles", showPositionHandles);
            EditorPrefs.SetBool("CCC Radius Handles", showRadiusHandles);
            EditorPrefs.SetBool("CCC Radius Gizmos", showRadiusGizmos);
            EditorPrefs.SetBool("CCC Hook Handles", showHookHandles);
            EditorPrefs.SetBool("CCC Hook Position Handles", showHookPositionHandles);
            EditorPrefs.SetBool("CCC Hook Rotation Handles", showHookRotationHandles);
            EditorPrefs.SetBool("CCC Hook Radius Handles", showHookRadiusHandles);
            EditorPrefs.SetBool("CCC Hook Strength Handles", showHookStrengthHandles);
            EditorPrefs.SetBool("CCC Hook Names", showHookNames);
        }

        //Load all editor window preferences (called when window is opened)
        private void LoadSettings()
        {
            reloadOnCompile = EditorPrefs.GetBool("CCC Reload Compile", true);
            guiWidthScale = EditorPrefs.GetFloat("CCC GUI Width", guiWidthScale);
            showOptions = EditorPrefs.GetBool("CCC Show Options", showOptions);
            showSaveOptions = EditorPrefs.GetBool("CCC Show Save Options", showSaveOptions);
            showTarget = EditorPrefs.GetBool("CCC Show Target", showTarget);
            showMirroringAndFlipping = EditorPrefs.GetBool("CCC Show Mirror", showMirroringAndFlipping);
            showCorners = EditorPrefs.GetBool("CCC Show Corners", showCorners);
            showCornerPositions = EditorPrefs.GetBool("CCC Show Corner Positions", showCornerPositions);
            showCornerRadii = EditorPrefs.GetBool("CCC Show Corner Radii", showCornerRadii);
            showCornerRadiusOffsets = EditorPrefs.GetBool("CCC Show Corner Radius Offsets", showCornerRadiusOffsets);
            showDetail = EditorPrefs.GetBool("CCC Show Detail", showDetail);
            showHooks = EditorPrefs.GetBool("CCC Show Hooks", showHooks);
            showResetButtons = EditorPrefs.GetBool("CCC Show Reset Buttons", showResetButtons);

            livePreview = EditorPrefs.GetBool("CCC Live Preview", livePreview);
            previewAllColliders = EditorPrefs.GetBool("CCC Preview All", previewAllColliders);
            sceneUpdateTime = EditorPrefs.GetFloat("CCC Preview Time", sceneUpdateTime);
            previewMode = (MeshPreviewMode)EditorPrefs.GetInt("CCC Preview Mode", (int)previewMode);

            targetIsSelection = EditorPrefs.GetBool("CCC Target Select", targetIsSelection);
            nullClearTarget = EditorPrefs.GetBool("CCC Null Select", nullClearTarget);

            allowDeletion = EditorPrefs.GetBool("CCC Allow Delete", allowDeletion);
            allowMirroring = EditorPrefs.GetBool("CCC Allow Mirror", allowMirroring);
            allowFlipping = EditorPrefs.GetBool("CCC Allow Flip", allowFlipping);

            showPositionHandles = EditorPrefs.GetBool("CCC Position Handles", showPositionHandles);
            showRadiusHandles = EditorPrefs.GetBool("CCC Radius Handles", showRadiusHandles);
            showRadiusGizmos = EditorPrefs.GetBool("CCC Radius Gizmos", showRadiusGizmos);
            showHookHandles = EditorPrefs.GetBool("CCC Hook Handles", showHookHandles);
            showHookPositionHandles = EditorPrefs.GetBool("CCC Hook Position Handles", showHookPositionHandles);
            showHookRotationHandles = EditorPrefs.GetBool("CCC Hook Rotation Handles", showHookRotationHandles);
            showHookRadiusHandles = EditorPrefs.GetBool("CCC Hook Radius Handles", showHookRadiusHandles);
            showHookStrengthHandles = EditorPrefs.GetBool("CCC Hook Strength Handles", showHookStrengthHandles);
            showHookNames = EditorPrefs.GetBool("CCC Hook Names", showHookNames);
        }

        //Called when the window is opened
        private void OnEnable()
        {
            window = this;
            LoadSettings();
            genProps = genPropsTemp;//Set the temporary property holder as the current collider properties for editing

            liveMat = new Material(Shader.Find(liveMatShader));
            liveWireMat = new Material(Shader.Find(liveMatWireShader));

            //Setting up the reorderable list for hooks
            sWin = new SerializedObject(this);
            sHooks = sWin.FindProperty("hooks");
            hookList = new ReorderableList(sWin, sHooks, true, true, true, true);
            float lineHeight = EditorGUIUtility.singleLineHeight * 1.2f;
            hookList.elementHeight = lineHeight * 10.0f;

            //Drawing the hook list
            hookList.drawHeaderCallback = (Rect rect) => EditorGUI.LabelField(rect, "Deform Hooks");
            hookList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                float elementWidth = 200f * guiWidthScale;
                SerializedProperty curHook = hookList.serializedProperty.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y, elementWidth, EditorGUIUtility.singleLineHeight),
                    curHook.FindPropertyRelative("showHandles"));
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y + lineHeight, 0.5f * elementWidth, lineHeight),
                    curHook.FindPropertyRelative("hookType"), GUIContent.none);
                EditorGUI.PropertyField(
                    new Rect(rect.x + elementWidth * 0.5f, rect.y + lineHeight, elementWidth, EditorGUIUtility.singleLineHeight),
                    curHook.FindPropertyRelative("enabled"));
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y + lineHeight * 2.0f, 300.0f * guiWidthScale, EditorGUIUtility.singleLineHeight),
                    curHook.FindPropertyRelative("name"), new GUIContent("Name"));
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y + lineHeight * 3.0f, 400.0f * guiWidthScale, lineHeight * 2.0f),
                    curHook.FindPropertyRelative("localPos"), new GUIContent("Local Position"));
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y + lineHeight * 5.0f, 400.0f * guiWidthScale, lineHeight * 2.0f),
                    curHook.FindPropertyRelative("localEulerRot"), new GUIContent("Local Rotation"));

                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y + lineHeight * 7.0f, 250f * guiWidthScale, EditorGUIUtility.singleLineHeight),
                    curHook.FindPropertyRelative("radius"));
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y + lineHeight * 8.0f, 250f * guiWidthScale, EditorGUIUtility.singleLineHeight),
                    curHook.FindPropertyRelative("strength"));
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y + lineHeight * 9.0f, 250f * guiWidthScale, EditorGUIUtility.singleLineHeight),
                    curHook.FindPropertyRelative("falloff"));
            };

            Undo.undoRedoPerformed += HookUndo;//Important function for undoing hook changes
            EditorSceneManager.sceneOpened += ReloadOnSceneChange;//Calls the reload function when the scene changes

            //Setting up foldout animations
            cEditFadeBoxP = new AnimBool(true);
            cEditFadeBoxP.valueChanged.AddListener(Repaint);
            cEditFadeSideP = new AnimBool(false);
            cEditFadeSideP.valueChanged.AddListener(Repaint);
            cEditFadeIndP = new AnimBool(false);
            cEditFadeIndP.valueChanged.AddListener(Repaint);

            cEditFadeUniR0 = new AnimBool(false);
            cEditFadeUniR0.valueChanged.AddListener(Repaint);
            cEditFadeIndR0 = new AnimBool(true);
            cEditFadeIndR0.valueChanged.AddListener(Repaint);
            cEditFadeAdvR0 = new AnimBool(false);
            cEditFadeAdvR0.valueChanged.AddListener(Repaint);

            cEditFadeUniR1 = new AnimBool(false);
            cEditFadeUniR1.valueChanged.AddListener(Repaint);
            cEditFadeIndR1 = new AnimBool(true);
            cEditFadeIndR1.valueChanged.AddListener(Repaint);
            cEditFadeAdvR1 = new AnimBool(false);
            cEditFadeAdvR1.valueChanged.AddListener(Repaint);

            cEditDetAll = new AnimBool(true);
            cEditDetAll.valueChanged.AddListener(Repaint);
            cEditDetInd = new AnimBool(false);
            cEditDetInd.valueChanged.AddListener(Repaint);
        }

        //Called when the window is closed
        private void OnDisable()
        {
            SaveSettings();
            if (liveMat != null)
            {
                DestroyImmediate(liveMat);//Destroy the material used for drawing preview meshes
            }

            if (liveWireMat != null)
            {
                DestroyImmediate(liveWireMat);
            }

            //Destroy preview meshes
            for (int i = 0; i < colMeshes.Count; i++)
            {
                if (colMeshes[i] != null)
                {
                    DestroyImmediate(colMeshes[i]);
                }
            }

            Undo.undoRedoPerformed -= HookUndo;//Remove hook undo function from undo delegate
            EditorSceneManager.sceneOpened -= ReloadOnSceneChange;//Remove reload function from scene load delegate

            //Clean up foldout animation listeners
            cEditFadeBoxP.valueChanged.RemoveAllListeners();
            cEditFadeSideP.valueChanged.RemoveAllListeners();
            cEditFadeIndP.valueChanged.RemoveAllListeners();
            cEditFadeUniR0.valueChanged.RemoveAllListeners();
            cEditFadeIndR0.valueChanged.RemoveAllListeners();
            cEditFadeAdvR0.valueChanged.RemoveAllListeners();
            cEditFadeUniR1.valueChanged.RemoveAllListeners();
            cEditFadeIndR1.valueChanged.RemoveAllListeners();
            cEditFadeAdvR1.valueChanged.RemoveAllListeners();
            cEditDetAll.valueChanged.RemoveAllListeners();
            cEditDetInd.valueChanged.RemoveAllListeners();
        }

        //Assists with making hooks work with undoing
        void HookUndo()
        {
            SetHooksFromProps();
        }

        //Copies the collider's hooks to the window's hook list for editirng
        void SetHooksFromProps()
        {
            hooks = (DeformHook[])genProps.hooks.Clone();
        }

        //Called by a collider group when opening the window to edit a specific collider
        public void SetTargetFromGroup(ColliderGroupTargetInfo info)
        {
            colGroupInfoTemp = info;
            settingTargetFromGroup = true;
        }

        private void OnGUI()
        {
            scrollView = EditorGUILayout.BeginScrollView(scrollView);

            //Setting up GUI styles and layouts
            GUIStyle boldFoldout = new GUIStyle(EditorStyles.foldout);
            boldFoldout.fontStyle = FontStyle.Bold;

            GUIStyle primaryButton = new GUIStyle(GUI.skin.button);
            primaryButton.fontSize = 12;
            primaryButton.fontStyle = FontStyle.Bold;
            primaryButton.fixedHeight = EditorGUIUtility.singleLineHeight * 2.0f;

            GUIStyle secondaryButton = new GUIStyle(EditorStyles.toolbarButton);
            secondaryButton.fontSize = 12;
            secondaryButton.fontStyle = FontStyle.Bold;
            secondaryButton.fixedHeight = EditorGUIUtility.singleLineHeight * 1.5f;

            GUIStyle noticeText = new GUIStyle(GUI.skin.label);
            noticeText.fontStyle = FontStyle.BoldAndItalic;

            GUIStyle boldText = new GUIStyle(GUI.skin.label);
            boldText.fontStyle = FontStyle.Bold;

            GUILayoutOption guiWidth = GUILayout.Width(200f * guiWidthScale);
            GUILayoutOption guiWidthWide = GUILayout.Width(300f * guiWidthScale);
            GUILayoutOption guiWidthWider = GUILayout.Width(400f * guiWidthScale);
            GUILayoutOption guiWidthNarrow = GUILayout.Width(120f * guiWidthScale);
            GUILayoutOption guiSliderWidth = GUILayout.Width(410f * guiWidthScale);

            float initialLabelWidth = EditorGUIUtility.labelWidth;

            targetPrev = target;
            colIndexPrev = colIndex;

            if (settingTargetFromGroup)
            {
                //Set target collider from a collider group's edit button
                settingTargetFromGroup = false;
                target = colGroupInfoTemp.target;
                colIndex = colGroupInfoTemp.index;
                targetGroupColIndex = colGroupInfoTemp.index;
                genProps = colGroupInfoTemp.group.colliders[colIndex].props;
                colGroupInfoTemp = new ColliderGroupTargetInfo();
                LimitMaxRadiusProps();
                SetHooksFromProps();
            }
            else if (targetIsSelection && ((Selection.activeGameObject != null && Selection.assetGUIDs.Length == 0) || nullClearTarget))
            {
                target = Selection.assetGUIDs.Length == 0 ? Selection.activeGameObject : null;
            }

            EditorGUILayout.Space();

            //Window and editing options
            showOptions = EditorGUILayout.Foldout(showOptions, "Editor Options", true, boldFoldout);
            if (showOptions)
            {
                EditorGUI.indentLevel++;
                EditorGUIUtility.labelWidth = 190f;
                reloadOnCompile = EditorGUILayout.Toggle("Reload Window On Compile", reloadOnCompile);
                EditorGUIUtility.labelWidth = initialLabelWidth;
                guiWidthScale = EditorGUILayout.Slider("GUI Width Scale", guiWidthScale, 0.85f, 2.0f, guiSliderWidth);
                previewMode = (MeshPreviewMode)EditorGUILayout.EnumPopup("Mesh Preview Mode", previewMode, guiWidthWide);
                GUILayout.BeginHorizontal(guiWidth);
                EditorGUIUtility.labelWidth = 110f;
                livePreview = EditorGUILayout.Toggle("Live Preview", livePreview);
                EditorGUIUtility.labelWidth = 140f;
                previewAllColliders = EditorGUILayout.Toggle("Preview All Colliders", previewAllColliders);
                //Live editing of collider components is disabled because it interfered with undoing
                //EditorGUIUtility.labelWidth = 140f;
                //liveEdit = EditorGUILayout.Toggle("Live Collider Edit", liveEdit);
                GUILayout.EndHorizontal();
                EditorGUIUtility.labelWidth = 170f;
                sceneUpdateTime = Mathf.Max(0.0f, EditorGUILayout.FloatField("Live Edit Update Time", sceneUpdateTime, guiWidthWide));
                EditorGUIUtility.labelWidth = initialLabelWidth;
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            //Target options
            showTarget = EditorGUILayout.Foldout(showTarget, "Target Object", true, boldFoldout);
            if (showTarget)
            {
                EditorGUI.indentLevel++;
                targetIsSelection = EditorGUILayout.Toggle("Target Is Selection", targetIsSelection);
                EditorGUI.indentLevel++;
                EditorGUIUtility.labelWidth = 200f;
                nullClearTarget = EditorGUILayout.Toggle("Null Selection Clears Target", nullClearTarget);
                useColliderGroup = EditorGUILayout.Toggle("Use Collider Group", useColliderGroup);
                EditorGUIUtility.labelWidth = initialLabelWidth;
                EditorGUI.indentLevel--;
                target = (GameObject)EditorGUILayout.ObjectField("Target Object", target, typeof(GameObject), true);
            }

            //Adjusting the target object by adding or removing a collider group
            int maxIndex = 0;
            if (target != null)
            {
                if (useColliderGroup)
                {
                    dontDeleteColliderGroup = false;
                    if (target.GetComponent<ColliderGroup>() != null)
                    {
                        if (colGroup == null)
                        {
                            colIndexPrev = -1;
                        }
                        colGroup = target.GetComponent<ColliderGroup>();
                    }
                    else
                    {
                        colGroup = null;
                    }

                    if (colGroup != null)
                    {
                        maxIndex = Mathf.Max(0, colGroup.colliders.Count);
                        Undo.RecordObject(colGroup, "Collider Group Window Change");
                    }
                }
                else
                {
                    if (target.GetComponent<ColliderGroup>() != null)
                    {
                        if (!dontDeleteColliderGroup)
                        {
                            if (EditorUtility.DisplayDialog("Collider Group Deletion", "Do you want to delete the collider group and its colliders? (No undo)", "Yes", "No"))
                            {
                                dontDeleteColliderGroup = false;
                                colGroup.DestroyComponent();
                                colGroup = null;
                            }
                            else
                            {
                                dontDeleteColliderGroup = true;
                            }
                        }
                    }
                    maxIndex = 0;
                }
            }
            else if (colGroup != null)
            {
                maxIndex = 0;
                genProps = genPropsTemp;
                SetHooksFromProps();
                colGroup = null;
            }

            //Rebuild preview meshes if the target object changes or the number of colliders changes
            if (colMeshes.Count != maxIndex + 1 || target != targetPrev)
            {
                RebuildPreviewMeshes(maxIndex);
            }

            //More target options and collider index setting
            if (showTarget && useColliderGroup)
            {
                EditorGUI.indentLevel++;
                GUILayout.BeginHorizontal(guiWidth);
                colIndex = Mathf.Clamp(EditorGUILayout.IntField("Collider Index", colIndex, guiWidth), 0, maxIndex);

                if (GUILayout.Button("<"))
                {
                    colIndex = Mathf.Clamp(colIndex - 1, 0, maxIndex);
                }

                if (GUILayout.Button(">"))
                {
                    colIndex = Mathf.Clamp(colIndex + 1, 0, maxIndex);
                }

                EditorGUILayout.LabelField("Max: " + maxIndex.ToString(), boldText, GUILayout.Width(100f * guiWidthScale));
                GUILayout.EndHorizontal();

                //This is to avoid layout mismatch errors bewtween the layout and repaint events, since the label appearing is conditional
                if (Event.current.type == EventType.Layout)
                {
                    drawMaxIndexLabel = colIndex == maxIndex;
                }

                if (drawMaxIndexLabel)
                {
                    EditorGUILayout.LabelField("Max index does not exist and must be generated.", noticeText, GUILayout.Width(400f));
                }
                EditorGUI.indentLevel--;
            }
            else
            {
                colIndex = Mathf.Clamp(colIndex, 0, maxIndex);
            }

            if (useColliderGroup && colGroup != null)
            {
                if (colGroup.loadUpdated)
                {
                    //Update preview meshes when a collider preset is loaded
                    LimitIndex(colGroup.colliders.Count);
                    RebuildPreviewMeshes(maxIndex);
                    colGroup.loadUpdated = false;
                }
            }

            if (target != targetPrev || colIndex != colIndexPrev)
            {
                if (target != targetPrev)
                {
                    //Reset the temporary collider property holder when the target object or collider index changes
                    colIndex = targetGroupColIndex > -1 ? targetGroupColIndex : 0;
                    genPropsTemp.ResetCorners();
                    genPropsTemp.ResetDetail();
                    genPropsTemp.hooks = new DeformHook[0];
                }
                UpdateColliderFromIndex(maxIndex);
            }

            targetGroupColIndex = -1;

            //More target options and buttons
            if (showTarget)
            {
                EditorGUI.indentLevel++;
                genProps.isTrigger = EditorGUILayout.Toggle("Is Trigger", genProps.isTrigger, guiWidth);
                genProps.physMat = (PhysicMaterial)EditorGUILayout.ObjectField("Material", genProps.physMat, typeof(PhysicMaterial), true);

                EditorGUILayout.Space();
                genProps.polyTestMode = (ColliderGenerator.PolygonTestMode)EditorGUILayout.EnumPopup("Polygon Limit Test", genProps.polyTestMode, guiWidthWide);
                EditorGUIUtility.labelWidth = 160f;
                genProps.bypassPolyTest = EditorGUILayout.Toggle("Bypass Polygon Test", genProps.bypassPolyTest);
                genProps.detailReduction = (GeneratorProps.DetailReductionMode)EditorGUILayout.EnumPopup("Auto Detail Reduction", genProps.detailReduction, guiWidthWide);
                EditorGUI.indentLevel++;
                EditorGUIUtility.labelWidth = 200f;
                genProps.detailReductionAttempts = Mathf.Max(EditorGUILayout.IntField("Detail Reduction Attempts", genProps.detailReductionAttempts, guiWidthWide), 1);
                EditorGUIUtility.labelWidth = initialLabelWidth;
                EditorGUI.indentLevel -= 2;
                EditorGUILayout.Space();

                GUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(target == null);
                if (!useColliderGroup || (useColliderGroup && colGroup != null))
                {
                    if (GUILayout.Button("Generate Collider", primaryButton, guiWidth))
                    {
                        GenerateCollider(true);
                    }
                }
                else if (useColliderGroup && colGroup == null)
                {
                    if (GUILayout.Button("Create Collider Group", primaryButton, guiWidth))
                    {
                        if (target.GetComponent<ColliderGroup>() == null)
                        {
                            colGroup = target.AddComponent<ColliderGroup>();
                        }
                        else
                        {
                            colGroup = target.GetComponent<ColliderGroup>();
                        }
                    }
                }
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(colIndex == maxIndex || !useColliderGroup || target == null);
                if (GUILayout.Button("Duplicate Collider", primaryButton, guiWidth))
                {
                    DuplicateCollider();
                }
                EditorGUI.EndDisabledGroup();
                GUILayout.EndHorizontal();

                EditorGUILayout.Space();
                GUILayout.BeginHorizontal();
                allowDeletion = EditorGUILayout.Toggle("Allow Deletion", allowDeletion, guiWidth);

                EditorGUI.BeginDisabledGroup((colIndex == maxIndex && useColliderGroup) || target == null || !allowDeletion);
                if (GUILayout.Button("Delete Collider", primaryButton, guiWidth))
                {
                    DeleteCollider();
                }
                EditorGUI.EndDisabledGroup();
                GUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            //Preset and mesh saving options
            showSaveOptions = EditorGUILayout.Foldout(showSaveOptions, "Preset/Mesh Saving", true, boldFoldout);
            if (showSaveOptions)
            {
                EditorGUI.indentLevel++;
                genProps.linkedPreset = (ColliderPreset)EditorGUILayout.ObjectField("Preset", genProps.linkedPreset, typeof(ColliderPreset), true);
                EditorGUILayout.Space();
                GUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(genProps.linkedPreset == null);
                if (GUILayout.Button("Save To Preset", secondaryButton, guiWidth))
                {
                    genProps.SavePropertiesToPreset();
                }

                if (GUILayout.Button("Load From Preset", secondaryButton, guiWidth))
                {
                    if (genProps.LoadPropertiesFromPreset())
                    {
                        SetHooksFromProps();
                    }

                    for (int i = 0; i < genProps.corners.Length; i++)
                    {
                        maxCornerRadius = Mathf.Max(maxCornerRadius, genProps.corners[i].axleRadii.x, genProps.corners[i].axleRadii.y, genProps.corners[i].axleRadii.z);
                        maxCornerOffset = Mathf.Max(maxCornerOffset, genProps.corners[i].radiusOffsets.x, genProps.corners[i].radiusOffsets.y, genProps.corners[i].radiusOffsets.z);
                    }
                }
                EditorGUI.EndDisabledGroup();
                GUILayout.EndHorizontal();
                EditorGUILayout.Space();

                genProps.meshAssetPath = EditorGUILayout.TextField("Mesh Path", genProps.meshAssetPath);
                EditorGUILayout.Space();
                EditorGUI.BeginDisabledGroup((colIndex == maxIndex && useColliderGroup) || target == null || colMeshes[colIndex] == null);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Save Mesh", secondaryButton, guiWidth))
                {
                    SaveMeshAsset(false);
                }

                if (GUILayout.Button("Save Mesh As...", secondaryButton, guiWidth))
                {
                    SaveMeshAsset(true);
                }
                GUILayout.EndHorizontal();
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            //Mirroring and flipping options
            showMirroringAndFlipping = EditorGUILayout.Foldout(showMirroringAndFlipping, "Mirroring/Flipping", true, boldFoldout);
            if (showMirroringAndFlipping)
            {
                EditorGUI.indentLevel++;
                allowFlipping = EditorGUILayout.Toggle("Allow Flipping", allowFlipping, GUILayout.ExpandWidth(false));
                EditorGUILayout.Space();

                EditorGUI.BeginDisabledGroup((colIndex == maxIndex && useColliderGroup) || target == null || !allowFlipping);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Flip X", secondaryButton, guiWidthNarrow))
                {
                    genProps.Flip(Vector3.right, ref hooks);
                }

                if (GUILayout.Button("Flip Y", secondaryButton, guiWidthNarrow))
                {
                    genProps.Flip(Vector3.up, ref hooks);
                }

                if (GUILayout.Button("Flip Z", secondaryButton, guiWidthNarrow))
                {
                    genProps.Flip(Vector3.forward, ref hooks);
                }
                GUILayout.EndHorizontal();
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.Space();

                allowMirroring = EditorGUILayout.Toggle("Allow Mirroring", allowMirroring, guiWidth);
                EditorGUILayout.Space();

                EditorGUI.BeginDisabledGroup((colIndex == maxIndex && useColliderGroup) || target == null || !allowMirroring);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Mirror +X", secondaryButton, guiWidthNarrow))
                {
                    genProps.Mirror(Vector3.right, ref hooks);
                }

                if (GUILayout.Button("Mirror -X", secondaryButton, guiWidthNarrow))
                {
                    genProps.Mirror(Vector3.left, ref hooks);
                }
                GUILayout.EndHorizontal();
                EditorGUILayout.Space();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Mirror +Y", secondaryButton, guiWidthNarrow))
                {
                    genProps.Mirror(Vector3.up, ref hooks);
                }

                if (GUILayout.Button("Mirror -Y", secondaryButton, guiWidthNarrow))
                {
                    genProps.Mirror(Vector3.down, ref hooks);
                }
                GUILayout.EndHorizontal();
                EditorGUILayout.Space();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Mirror +Z", secondaryButton, guiWidthNarrow))
                {
                    genProps.Mirror(Vector3.forward, ref hooks);
                }

                if (GUILayout.Button("Mirror -Z", secondaryButton, guiWidthNarrow))
                {
                    genProps.Mirror(Vector3.back, ref hooks);
                }
                GUILayout.EndHorizontal();
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            //Foldout animation states
            cEditFadeBoxP.target = genProps.cornerPositionMode == GeneratorProps.CornerPositionEditMode.BoxCenter;
            cEditFadeSideP.target = genProps.cornerPositionMode == GeneratorProps.CornerPositionEditMode.BoxSides;
            cEditFadeIndP.target = genProps.cornerPositionMode == GeneratorProps.CornerPositionEditMode.Individual;

            cEditFadeUniR0.target = genProps.cornerRadiusMode == GeneratorProps.CornerRadiusEditMode.Uniform;
            cEditFadeIndR0.target = genProps.cornerRadiusMode == GeneratorProps.CornerRadiusEditMode.UniformIndividual;
            cEditFadeAdvR0.target = genProps.cornerRadiusMode == GeneratorProps.CornerRadiusEditMode.Advanced;

            cEditFadeUniR1.target = genProps.cornerOffsetMode == GeneratorProps.CornerRadiusEditMode.Uniform;
            cEditFadeIndR1.target = genProps.cornerOffsetMode == GeneratorProps.CornerRadiusEditMode.UniformIndividual;
            cEditFadeAdvR1.target = genProps.cornerOffsetMode == GeneratorProps.CornerRadiusEditMode.Advanced;

            //Collider corner options
            showCorners = EditorGUILayout.Foldout(showCorners, "Corners", true, boldFoldout);
            if (showCorners)
            {
                EditorGUI.indentLevel++;
                //Corner position options
                showCornerPositions = EditorGUILayout.Foldout(showCornerPositions, "Positions", true, boldFoldout);
                if (showCornerPositions)
                {
                    EditorGUI.indentLevel++;
                    showPositionHandles = EditorGUILayout.Toggle("Position Handles", showPositionHandles);
                    cPosModePrev = genProps.cornerPositionMode;
                    EditorGUIUtility.labelWidth = 160f;
                    genProps.cornerPositionMode = (GeneratorProps.CornerPositionEditMode)EditorGUILayout.EnumPopup("Position Edit Mode", genProps.cornerPositionMode, guiWidthWide);
                    EditorGUIUtility.labelWidth = initialLabelWidth;

                    if (genProps.cornerPositionMode != cPosModePrev)
                    {
                        if (genProps.cornerPositionMode == GeneratorProps.CornerPositionEditMode.BoxCenter)
                        {
                            genProps.boxSize = genProps.boxSidesPos - genProps.boxSidesNeg;
                            genProps.boxOffset = (genProps.boxSidesPos + genProps.boxSidesNeg) * 0.5f;
                        }
                        else if (genProps.cornerPositionMode == GeneratorProps.CornerPositionEditMode.BoxSides)
                        {
                            genProps.boxSidesPos = genProps.boxOffset + genProps.boxSize * 0.5f;
                            genProps.boxSidesNeg = genProps.boxOffset - genProps.boxSize * 0.5f;
                        }
                    }

                    //Corner positioning based on box and center properties
                    if (EditorGUILayout.BeginFadeGroup(cEditFadeBoxP.faded) && genProps.cornerPositionMode == GeneratorProps.CornerPositionEditMode.BoxCenter)
                    {
                        Vector3 setBox = EditorGUILayout.Vector3Field("Box Size", genProps.boxSize, guiWidthWider);
                        genProps.boxSize = new Vector3(Mathf.Max(0.0f, setBox.x), Mathf.Max(0.0f, setBox.y), Mathf.Max(0.0f, setBox.z));
                        genProps.boxOffset = EditorGUILayout.Vector3Field("Box Offset", genProps.boxOffset, guiWidthWider);
                        genProps.SetCornerPositionsFromBox();
                    }
                    EditorGUILayout.EndFadeGroup();

                    //Corner positioning based on box side properties
                    if (EditorGUILayout.BeginFadeGroup(cEditFadeSideP.faded) && genProps.cornerPositionMode == GeneratorProps.CornerPositionEditMode.BoxSides)
                    {
                        genProps.boxSidesPos = EditorGUILayout.Vector3Field("Positive Box Side Extents", genProps.boxSidesPos, guiWidthWider);
                        genProps.boxSidesNeg = EditorGUILayout.Vector3Field("Negative Box Side Extents", genProps.boxSidesNeg, guiWidthWider);
                        genProps.SetCornerPositionsFromBox();
                    }
                    EditorGUILayout.EndFadeGroup();

                    //Free-form corner positioning
                    if (EditorGUILayout.BeginFadeGroup(cEditFadeIndP.faded))
                    {
                        for (int i = 0; i < genProps.corners.Length; i++)
                        {
                            ColliderCorner curCorner = genProps.corners[i];
                            curCorner.localPos = EditorGUILayout.Vector3Field(curCorner.cornerLocation.ToString(), curCorner.localPos, guiWidthWider);
                        }
                    }
                    EditorGUILayout.EndFadeGroup();
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

                //Corner radius options
                showCornerRadii = EditorGUILayout.Foldout(showCornerRadii, "Radii", true, boldFoldout);
                if (showCornerRadii)
                {
                    EditorGUI.indentLevel++;
                    GUILayout.BeginHorizontal(guiWidth);
                    showRadiusHandles = EditorGUILayout.Toggle("Radius Handles", showRadiusHandles);
                    showRadiusGizmos = EditorGUILayout.Toggle("Radius Gizmos", showRadiusGizmos);
                    GUILayout.EndHorizontal();

                    EditorGUIUtility.labelWidth = 170f;
                    cornerHandleMode = (CornerHandleEditMode)EditorGUILayout.EnumPopup("Radius Handle Mode", cornerHandleMode, guiWidthWide);
                    EditorGUIUtility.labelWidth = initialLabelWidth;

                    genProps.cornerRadiusMode = (GeneratorProps.CornerRadiusEditMode)EditorGUILayout.EnumPopup("Radius Edit Mode", genProps.cornerRadiusMode, guiWidthWide);
                    maxCornerRadius = Mathf.Max(0.0f, EditorGUILayout.FloatField("Max Radius", maxCornerRadius, guiWidthWide));

                    //Setting all radii to be the same
                    if (EditorGUILayout.BeginFadeGroup(cEditFadeUniR0.faded) && genProps.cornerRadiusMode == GeneratorProps.CornerRadiusEditMode.Uniform)
                    {
                        EditorGUIUtility.labelWidth = 160f;
                        float allRadii = EditorGUILayout.Slider("Corner Radii", genProps.corners[0].axleRadii.x, 0.0f, maxCornerRadius, guiSliderWidth);
                        for (int i = 0; i < genProps.corners.Length; i++)
                        {
                            genProps.corners[i].axleRadii = Vector3.one * allRadii;
                        }
                        EditorGUIUtility.labelWidth = initialLabelWidth;
                    }
                    EditorGUILayout.EndFadeGroup();

                    //Setting all radii to be unique
                    if (EditorGUILayout.BeginFadeGroup(cEditFadeIndR0.faded) && genProps.cornerRadiusMode == GeneratorProps.CornerRadiusEditMode.UniformIndividual)
                    {
                        EditorGUIUtility.labelWidth = 160f;
                        for (int i = 0; i < genProps.corners.Length; i++)
                        {
                            ColliderCorner curCorner = genProps.corners[i];
                            curCorner.axleRadii = Vector3.one * EditorGUILayout.Slider(curCorner.cornerLocation.ToString(), curCorner.axleRadii.x, 0.0f, maxCornerRadius, guiSliderWidth);
                        }
                        EditorGUIUtility.labelWidth = initialLabelWidth;
                    }
                    EditorGUILayout.EndFadeGroup();

                    //Setting all components of radii to be unique
                    if (EditorGUILayout.BeginFadeGroup(cEditFadeAdvR0.faded))
                    {
                        for (int i = 0; i < genProps.corners.Length; i++)
                        {
                            ColliderCorner curCorner = genProps.corners[i];
                            Vector3 setRadii = EditorGUILayout.Vector3Field(curCorner.cornerLocation.ToString(), curCorner.axleRadii, guiWidthWider);
                            curCorner.axleRadii = new Vector3(Mathf.Clamp(setRadii.x, 0.0f, maxCornerRadius), Mathf.Clamp(setRadii.y, 0.0f, maxCornerRadius), Mathf.Clamp(setRadii.z, 0.0f, maxCornerRadius));
                        }
                    }
                    EditorGUILayout.EndFadeGroup();
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

                //Corner radius offset options
                showCornerRadiusOffsets = EditorGUILayout.Foldout(showCornerRadiusOffsets, "Radius Offsets", true, boldFoldout);
                if (showCornerRadiusOffsets)
                {
                    EditorGUI.indentLevel++;
                    genProps.cornerOffsetMode = (GeneratorProps.CornerRadiusEditMode)EditorGUILayout.EnumPopup("Offset Edit Mode", genProps.cornerOffsetMode, guiWidthWide);
                    maxCornerOffset = Mathf.Max(0.0f, EditorGUILayout.FloatField("Max Offset", maxCornerOffset, guiWidth));

                    //All radius offsets same
                    if (EditorGUILayout.BeginFadeGroup(cEditFadeUniR1.faded) && genProps.cornerOffsetMode == GeneratorProps.CornerRadiusEditMode.Uniform)
                    {
                        EditorGUIUtility.labelWidth = 160f;
                        float allOffsets = EditorGUILayout.Slider("Radius Offsets", genProps.corners[0].radiusOffsets.x, 0.0f, maxCornerOffset, guiSliderWidth);
                        for (int i = 0; i < genProps.corners.Length; i++)
                        {
                            genProps.corners[i].radiusOffsets = Vector3.one * allOffsets;
                        }
                        EditorGUIUtility.labelWidth = initialLabelWidth;
                    }
                    EditorGUILayout.EndFadeGroup();

                    //All radius offsets unique
                    if (EditorGUILayout.BeginFadeGroup(cEditFadeIndR1.faded) && genProps.cornerOffsetMode == GeneratorProps.CornerRadiusEditMode.UniformIndividual)
                    {
                        EditorGUIUtility.labelWidth = 160f;
                        for (int i = 0; i < genProps.corners.Length; i++)
                        {
                            ColliderCorner curCorner = genProps.corners[i];
                            curCorner.radiusOffsets = Vector3.one * EditorGUILayout.Slider(curCorner.cornerLocation.ToString(), curCorner.radiusOffsets.x, 0.0f, maxCornerOffset, guiSliderWidth);
                        }
                        EditorGUIUtility.labelWidth = initialLabelWidth;
                    }
                    EditorGUILayout.EndFadeGroup();

                    //All components of radius offsets unique
                    if (EditorGUILayout.BeginFadeGroup(cEditFadeAdvR1.faded))
                    {
                        for (int i = 0; i < genProps.corners.Length; i++)
                        {
                            ColliderCorner curCorner = genProps.corners[i];
                            Vector3 setOffsets = EditorGUILayout.Vector3Field(curCorner.cornerLocation.ToString(), curCorner.radiusOffsets, guiWidthWider);
                            curCorner.radiusOffsets = new Vector3(Mathf.Clamp(setOffsets.x, 0.0f, maxCornerOffset), Mathf.Clamp(setOffsets.y, 0.0f, maxCornerOffset), Mathf.Clamp(setOffsets.z, 0.0f, maxCornerOffset));
                        }
                    }
                    EditorGUILayout.EndFadeGroup();
                    EditorGUI.indentLevel--;
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            //More foldout animation states
            cEditDetAll.target = genProps.cornerDetailMode == GeneratorProps.CornerDetailEditMode.All;
            cEditDetInd.target = genProps.cornerDetailMode == GeneratorProps.CornerDetailEditMode.Individual;

            //Detail options
            showDetail = EditorGUILayout.Foldout(showDetail, "Detail", true, boldFoldout);
            if (showDetail)
            {
                EditorGUI.indentLevel++;
                EditorGUIUtility.labelWidth = 160f;
                genProps.cornerDetailMode = (GeneratorProps.CornerDetailEditMode)EditorGUILayout.EnumPopup("Corner Detail Edit Mode", genProps.cornerDetailMode, guiWidthWide);
                EditorGUIUtility.labelWidth = initialLabelWidth;

                EditorGUI.indentLevel++;
                //All corner details are the same
                if (EditorGUILayout.BeginFadeGroup(cEditDetAll.faded))
                {
                    int setDetail = EditorGUILayout.IntSlider("Detail All Corners", genProps.cornerDetails[0], 0, 10, guiSliderWidth);
                    genProps.cornerDetails[0] = setDetail;
                    genProps.cornerDetails[1] = setDetail;
                    genProps.cornerDetails[2] = setDetail;
                    genProps.cornerDetails[3] = setDetail;
                }
                EditorGUILayout.EndFadeGroup();

                //All corner details are unique
                if (EditorGUILayout.BeginFadeGroup(cEditDetInd.faded))
                {
                    genProps.cornerDetails[0] = EditorGUILayout.IntSlider("Detail Front Right", genProps.cornerDetails[0], 0, 10, guiSliderWidth);
                    genProps.cornerDetails[1] = EditorGUILayout.IntSlider("Detail Front Left", genProps.cornerDetails[1], 0, 10, guiSliderWidth);
                    genProps.cornerDetails[2] = EditorGUILayout.IntSlider("Detail Back Left", genProps.cornerDetails[2], 0, 10, guiSliderWidth);
                    genProps.cornerDetails[3] = EditorGUILayout.IntSlider("Detail Back Right", genProps.cornerDetails[3], 0, 10, guiSliderWidth);
                }
                EditorGUILayout.EndFadeGroup();
                EditorGUI.indentLevel--;

                EditorGUILayout.Space();
                //Other detail properties
                genProps.topSegments = EditorGUILayout.IntSlider("Top Segments", genProps.topSegments, 0, 10, guiSliderWidth);
                genProps.bottomSegments = EditorGUILayout.IntSlider("Bottom Segments", genProps.bottomSegments, 0, 10, guiSliderWidth);
                EditorGUILayout.Space();
                genProps.XYDetail = EditorGUILayout.IntSlider("X-Y Plane Strips", genProps.XYDetail, 0, 10, guiSliderWidth);
                genProps.YZDetail = EditorGUILayout.IntSlider("Y-Z Plane Strips", genProps.YZDetail, 0, 10, guiSliderWidth);
                genProps.XZDetail = EditorGUILayout.IntSlider("X-Z Plane Strips", genProps.XZDetail, 0, 10, guiSliderWidth);
                EditorGUILayout.Space();
                genProps.detailSmoothness = EditorGUILayout.Slider("Detail Smoothness", genProps.detailSmoothness, 0.0f, 1.0f, guiSliderWidth);
                EditorGUILayout.Space();
                EditorGUIUtility.labelWidth = 160f;
                genProps.stripDistribution1 = EditorGUILayout.Slider("Upper Strip Distribution", genProps.stripDistribution1, 0.01f, 2.0f, guiSliderWidth);
                genProps.stripDistribution2 = EditorGUILayout.Slider("Lower Strip Distribution", genProps.stripDistribution2, 0.01f, 2.0f, guiSliderWidth);
                EditorGUIUtility.labelWidth = initialLabelWidth;
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            //Hook options
            showHooks = EditorGUILayout.Foldout(showHooks, "Deform Hooks", boldFoldout);
            if (showHooks)
            {
                EditorGUI.indentLevel++;
                showHookHandles = EditorGUILayout.Toggle("Show Hook Handles", showHookHandles);
                if (showHookHandles)
                {
                    EditorGUI.indentLevel++;
                    GUILayout.BeginHorizontal(guiWidth);
                    showHookPositionHandles = EditorGUILayout.Toggle("Position Handles", showHookPositionHandles);
                    showHookRotationHandles = EditorGUILayout.Toggle("Rotation Handles", showHookRotationHandles);
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal(guiWidth);
                    showHookRadiusHandles = EditorGUILayout.Toggle("Radius Handles", showHookRadiusHandles);
                    EditorGUIUtility.labelWidth = 180f;
                    showHookStrengthHandles = EditorGUILayout.Toggle("Strength/Falloff Handles", showHookStrengthHandles);
                    GUILayout.EndHorizontal();
                    EditorGUIUtility.labelWidth = 120f;
                    showHookNames = EditorGUILayout.Toggle("Hook Names", showHookNames);
                    EditorGUIUtility.labelWidth = initialLabelWidth;
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.Space();

                if (hooks != null)
                {
                    for (int i = 0; i < hooks.Length; i++)
                    {
                        if (!hooks[i].set)
                        {
                            //Setting up new hooks that have not been configured
                            hooks[i].Initialize();
                        }
                    }

                    if (GUI.changed)
                    {
                        for (int i = 0; i < hooks.Length; i++)
                        {
                            //Update hook euler angles in window from hook quaternion rotation
                            hooks[i].localEulerRot = hooks[i].localRot.eulerAngles;
                        }
                    }

                    //Actual updating of hook properties
                    sWin.Update();
                    hookList.DoLayoutList();
                    sWin.ApplyModifiedProperties();

                    if (GUI.changed)
                    {
                        for (int i = 0; i < hooks.Length; i++)
                        {
                            //Set hook quaternion rotation from euler angles in window if they were changed
                            hooks[i].SetRotationFromEuler();
                        }
                    }
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            //Buttons for resetting different properties
            showResetButtons = EditorGUILayout.Toggle("Enable Reset Buttons", showResetButtons);
            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(!showResetButtons);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Box/Corners", secondaryButton))
            {
                genProps.ResetCorners();
            }

            if (GUILayout.Button("Reset Detail", secondaryButton))
            {
                genProps.ResetDetail();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Hooks", secondaryButton) && hooks != null)
            {
                for (int i = 0; i < hooks.Length; i++)
                {
                    hooks[i].Reset();
                }
            }

            if (GUILayout.Button("Delete All Hooks", secondaryButton) && hooks != null)
            {
                genProps.hooks = new DeformHook[0];
                SetHooksFromProps();
            }
            GUILayout.EndHorizontal();

            EditorGUI.BeginDisabledGroup(target == null);
            if (GUILayout.Button("Delete All Colliders", secondaryButton))
            {
                DeleteAllColliders();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space();

            EditorGUILayout.EndScrollView();

            if (GUI.changed && target != null && useColliderGroup && colGroup != null)
            {
                //Let the Unity editor know if changed properties need ot be saved
                EditorUtility.SetDirty(colGroup);
            }
        }

        private void OnFocus()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDestroy()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private void OnSceneGUI(SceneView sv)
        {
            if (target == null)
            {
                return;
            }

            if (useColliderGroup && colGroup != null)
            {
                Undo.RecordObject(colGroup, "Collider Group Scene Change");
            }

            Vector3 tPos = target.transform.position;
            Quaternion tRot = target.transform.rotation;
            float handleSize = HandleUtility.GetHandleSize(target.transform.TransformPoint(genProps.GetAverageCornerPos()));
            Vector3 screenNormal = HandleUtility.GUIPointToWorldRay(new Vector2(Screen.width / 2, Screen.height / 2)).direction;
            Matrix4x4 m = target.transform.localToWorldMatrix;//Matrix for transforming handles to match target object's orientation
            m.SetColumn(0, m.GetColumn(0).normalized);//Normalizing the scale of the matrix so that the handles are not distorted
            m.SetColumn(1, m.GetColumn(1).normalized);
            m.SetColumn(2, m.GetColumn(2).normalized);
            Handles.matrix = m;

            if (previewMode == MeshPreviewMode.RealMesh)
            {
                //Mesh preview drawing
                if (liveMat != null && liveWireMat != null)
                {
                    if (previewAllColliders && useColliderGroup && colGroup != null)
                    {
                        //Wireframe drawing for all colliders
                        GL.wireframe = true;
                        for (int i = 0; i < colMeshes.Count; i++)
                        {
                            if ((i != colIndex || !livePreview) && colMeshes[i] != null)
                            {
                                for (int j = 0; j < liveWireMat.passCount; j++)
                                {
                                    if (liveWireMat.SetPass(j))
                                    {
                                        Graphics.DrawMeshNow(colMeshes[i], target.transform.localToWorldMatrix);
                                    }
                                }
                            }
                        }
                        GL.wireframe = false;
                    }

                    if (colMeshes[colIndex] != null)
                    {
                        //Face drawing for active collider
                        for (int i = 0; i < liveMat.passCount; i++)
                        {
                            if (liveMat.SetPass(i))
                            {
                                Graphics.DrawMeshNow(colMeshes[colIndex], target.transform.localToWorldMatrix);
                            }
                        }

                        //Wireframe drawing for active collider
                        GL.wireframe = true;
                        for (int i = 0; i < liveWireMat.passCount; i++)
                        {
                            if (liveWireMat.SetPass(i))
                            {
                                Graphics.DrawMeshNow(colMeshes[colIndex], target.transform.localToWorldMatrix);
                            }
                        }
                        GL.wireframe = false;
                    }
                }
            }
            else if (previewMode == MeshPreviewMode.BoxApproximation)
            {
                //Approximate visualization of collider previews
                if (previewAllColliders && useColliderGroup && colGroup != null)
                {
                    for (int i = 0; i < colGroup.colliders.Count; i++)
                    {
                        if (colGroup.colliders[i] != null && i != colIndex)
                        {
                            DrawColliderApproximation(colGroup.colliders[i].props, false);
                        }
                    }
                }

                //Drawing approximation for active collider with solid faces
                DrawColliderApproximation(genProps, true);
            }

            if (showPositionHandles)
            {
                if (genProps.cornerPositionMode == GeneratorProps.CornerPositionEditMode.BoxCenter)
                {
                    //Position handles for box with center mode
                    Handles.color = Handles.xAxisColor;
                    float offsetX = (Handles.Slider(genProps.boxOffset + Vector3.right * handleSize * 2.2f, Vector3.right, handleSize * 0.3f, Handles.ConeHandleCap, 0.5f)
                        - Vector3.right * handleSize * 2.2f).x;

                    Handles.color = Handles.yAxisColor;
                    float offsetY = (Handles.Slider(genProps.boxOffset + Vector3.up * handleSize * 2.2f, Vector3.up, handleSize * 0.3f, Handles.ConeHandleCap, 0.5f)
                        - Vector3.up * handleSize * 2.2f).y;

                    Handles.color = Handles.zAxisColor;
                    float offsetZ = (Handles.Slider(genProps.boxOffset + Vector3.forward * handleSize * 2.2f, Vector3.forward, handleSize * 0.3f, Handles.ConeHandleCap, 0.5f)
                        - Vector3.forward * handleSize * 2.2f).z;

                    genProps.boxOffset.Set(offsetX, offsetY, offsetZ);
                    Vector3 minBoxSize = new Vector3(Mathf.Max(0.01f, genProps.boxSize.x), Mathf.Max(0.01f, genProps.boxSize.y), Mathf.Max(0.01f, genProps.boxSize.z));
                    genProps.boxSize = Handles.ScaleHandle(minBoxSize, genProps.boxOffset, Quaternion.identity, handleSize * 1.5f);

                    if (!showCorners || !showCornerPositions)
                    {
                        genProps.SetCornerPositionsFromBox();
                    }
                }
                else if (genProps.cornerPositionMode == GeneratorProps.CornerPositionEditMode.BoxSides)
                {
                    //Position handles for box sides mode
                    Vector3 xOffset = new Vector3(0.0f, (genProps.boxSidesPos.y + genProps.boxSidesNeg.y) * 0.5f, (genProps.boxSidesPos.z + genProps.boxSidesNeg.z) * 0.5f);
                    Handles.color = Handles.xAxisColor;
                    float sideXPos = Handles.Slider(xOffset + Vector3.right * genProps.boxSidesPos.x, Vector3.right, handleSize, Handles.ArrowHandleCap, 0.5f).x;
                    float sideXNeg = Handles.Slider(xOffset + Vector3.right * genProps.boxSidesNeg.x, Vector3.left, handleSize, Handles.ArrowHandleCap, 0.5f).x;
                    Handles.DrawWireDisc(xOffset + Vector3.right * genProps.boxSidesPos.x, Vector3.right, handleSize * 0.2f);
                    Handles.DrawWireDisc(xOffset + Vector3.right * genProps.boxSidesNeg.x, Vector3.left, handleSize * 0.2f);

                    Vector3 yOffset = new Vector3((genProps.boxSidesPos.x + genProps.boxSidesNeg.x) * 0.5f, 0.0f, (genProps.boxSidesPos.z + genProps.boxSidesNeg.z) * 0.5f);
                    Handles.color = Handles.yAxisColor;
                    float sideYPos = Handles.Slider(yOffset + Vector3.up * genProps.boxSidesPos.y, Vector3.up, handleSize, Handles.ArrowHandleCap, 0.5f).y;
                    float sideYNeg = Handles.Slider(yOffset + Vector3.up * genProps.boxSidesNeg.y, Vector3.down, handleSize, Handles.ArrowHandleCap, 0.5f).y;
                    Handles.DrawWireDisc(yOffset + Vector3.up * genProps.boxSidesPos.y, Vector3.up, handleSize * 0.2f);
                    Handles.DrawWireDisc(yOffset + Vector3.up * genProps.boxSidesNeg.y, Vector3.down, handleSize * 0.2f);

                    Vector3 zOffset = new Vector3((genProps.boxSidesPos.x + genProps.boxSidesNeg.x) * 0.5f, (genProps.boxSidesPos.y + genProps.boxSidesNeg.y) * 0.5f, 0.0f);
                    Handles.color = Handles.zAxisColor;
                    float sideZPos = Handles.Slider(zOffset + Vector3.forward * genProps.boxSidesPos.z, Vector3.forward, handleSize, Handles.ArrowHandleCap, 0.5f).z;
                    float sideZNeg = Handles.Slider(zOffset + Vector3.forward * genProps.boxSidesNeg.z, Vector3.back, handleSize, Handles.ArrowHandleCap, 0.5f).z;
                    Handles.DrawWireDisc(zOffset + Vector3.forward * genProps.boxSidesPos.z, Vector3.forward, handleSize * 0.2f);
                    Handles.DrawWireDisc(zOffset + Vector3.forward * genProps.boxSidesNeg.z, Vector3.back, handleSize * 0.2f);

                    genProps.boxSidesPos.Set(sideXPos, sideYPos, sideZPos);
                    genProps.boxSidesNeg.Set(sideXNeg, sideYNeg, sideZNeg);

                    if (!showCorners || !showCornerPositions)
                    {
                        genProps.SetCornerPositionsFromBox();
                    }
                }
                else if (genProps.cornerPositionMode == GeneratorProps.CornerPositionEditMode.Individual)
                {
                    //Free-form position handles for corners
                    for (int i = 0; i < genProps.corners.Length; i++)
                    {
                        ColliderCorner curCorner = genProps.corners[i];
                        if (Tools.current == Tool.Move)
                        {
                            curCorner.localPos = Handles.PositionHandle(curCorner.localPos, Quaternion.identity);
                        }
                    }
                }
            }

            bool editingRadii = cornerHandleMode == CornerHandleEditMode.Radii;
            Vector3 cornerRadProp;
            Vector3 cornerRadProp0;
            float propLimit = editingRadii ? maxCornerRadius : maxCornerOffset;
            GeneratorProps.CornerRadiusEditMode cornerModeCheck = genProps.cornerRadiusMode;
            float radHandleScale = 1.0f;
            if (!editingRadii)
            {
                cornerModeCheck = genProps.cornerOffsetMode;
                radHandleScale = 0.3f;
            }

            //Handles for corner radii
            for (int i = 0; i < genProps.corners.Length; i++)
            {
                ColliderCorner curCorner = genProps.corners[i];
                cornerRadProp = editingRadii ? curCorner.axleRadii : curCorner.radiusOffsets;
                handleSize = HandleUtility.GetHandleSize(curCorner.localPos);
                if (showRadiusGizmos)
                {
                    //Visualizations of corner radii
                    if (genProps.cornerRadiusMode != GeneratorProps.CornerRadiusEditMode.Advanced)
                    {
                        Handles.color = new Color(1.0f, 0.2f, 0.0f);
                        Handles.DrawWireDisc(curCorner.localPos - curCorner.GetOffset(), target.transform.InverseTransformDirection(screenNormal), curCorner.axleRadii.x);
                    }
                    else
                    {
                        Handles.color = Handles.xAxisColor;
                        Handles.DrawWireDisc(curCorner.localPos - curCorner.GetOffset() + Vector3.right * curCorner.axleRadii.x * curCorner.normalizedCornerLocation.x, Vector3.right, Mathf.Min(curCorner.axleRadii.y, curCorner.axleRadii.z));
                        Handles.color = Handles.yAxisColor;
                        Handles.DrawWireDisc(curCorner.localPos - curCorner.GetOffset() + Vector3.up * curCorner.axleRadii.y * curCorner.normalizedCornerLocation.y, Vector3.up, Mathf.Min(curCorner.axleRadii.x, curCorner.axleRadii.z));
                        Handles.color = Handles.zAxisColor;
                        Handles.DrawWireDisc(curCorner.localPos - curCorner.GetOffset() + Vector3.forward * curCorner.axleRadii.z * curCorner.normalizedCornerLocation.z, Vector3.forward, Mathf.Min(curCorner.axleRadii.x, curCorner.axleRadii.y));
                    }
                }

                if (showRadiusHandles)
                {
                    Handles.color = new Color(1.0f, 0.7f, 0.0f);
                    if (cornerModeCheck == GeneratorProps.CornerRadiusEditMode.Uniform)
                    {
                        //Each handle adjusts the radius for all corners simultaneously
                        cornerRadProp0 = editingRadii ? genProps.corners[0].axleRadii : genProps.corners[0].radiusOffsets;
                        Vector3 updatedProp = Vector3.one * Handles.SnapValue(Handles.RadiusHandle(Quaternion.identity, curCorner.localPos, cornerRadProp0.x * radHandleScale) / radHandleScale, 0.0f);
                        genProps.corners[0].SetRadiusProperty(updatedProp, editingRadii, propLimit);
                        curCorner.SetRadiusProperty(updatedProp, editingRadii, propLimit);
                    }
                    else if (cornerModeCheck == GeneratorProps.CornerRadiusEditMode.UniformIndividual)
                    {
                        //Each corner's radius is adjusted individually
                        curCorner.SetRadiusProperty(Vector3.one * Handles.SnapValue(Handles.RadiusHandle(Quaternion.identity, curCorner.localPos, cornerRadProp.x * radHandleScale) / radHandleScale, 0.0f), editingRadii, propLimit);
                    }
                    else if (cornerModeCheck == GeneratorProps.CornerRadiusEditMode.Advanced)
                    {
                        //Each of component of every corner is adjusted with handles
                        Vector3 center = curCorner.localPos;
                        Vector3 handlePointX = center + Vector3.right * cornerRadProp.x * curCorner.normalizedCornerLocation.x;
                        Vector3 handlePointY = center + Vector3.up * cornerRadProp.y * curCorner.normalizedCornerLocation.y;
                        Vector3 handlePointZ = center + Vector3.forward * cornerRadProp.z * curCorner.normalizedCornerLocation.z;

                        Handles.color = Handles.xAxisColor;
                        Handles.DrawLine(center, handlePointX);
                        float xRad = Handles.Slider(handlePointX, Vector3.right, handleSize * 0.2f, Handles.CubeHandleCap, 0.1f).x - center.x;
                        if (curCorner.normalizedCornerLocation.x < 0)
                        {
                            xRad *= -1.0f;
                        }

                        Handles.color = Handles.yAxisColor;
                        Handles.DrawLine(center, handlePointY);
                        float yRad = Handles.Slider(handlePointY, Vector3.up, handleSize * 0.2f, Handles.CubeHandleCap, 0.1f).y - center.y;
                        if (curCorner.normalizedCornerLocation.y < 0)
                        {
                            yRad *= -1.0f;
                        }

                        Handles.color = Handles.zAxisColor;
                        Handles.DrawLine(center, handlePointZ);
                        float zRad = Handles.Slider(handlePointZ, Vector3.forward, handleSize * 0.2f, Handles.CubeHandleCap, 0.1f).z - center.z;
                        if (curCorner.normalizedCornerLocation.z < 0)
                        {
                            zRad *= -1.0f;
                        }

                        if (GUI.changed)
                        {
                            curCorner.SetRadiusProperty(new Vector3(xRad, yRad, zRad), editingRadii, propLimit);
                        }
                    }
                }
            }

            if (hooks != null && showHookHandles)
            {
                //Handles for modifying hooks
                GUIStyle hookLabel = new GUIStyle(EditorStyles.helpBox);
                hookLabel.fontStyle = FontStyle.Bold;
                hookLabel.fontSize = 12;
                hookLabel.alignment = TextAnchor.MiddleCenter;
                hookLabel.padding = new RectOffset(1, 1, 1, 1);
                for (int i = 0; i < hooks.Length; i++)
                {
                    DeformHook curHook = hooks[i];
                    if (!curHook.set)
                    {
                        //Setting up new hooks that have not been configured
                        curHook.Initialize();
                    }

                    if (curHook.showHandles)
                    {
                        handleSize = HandleUtility.GetHandleSize(curHook.localPos);
                        if (showHookPositionHandles)
                        {
                            //Positioning hooks
                            curHook.localPos = Handles.PositionHandle(curHook.localPos, curHook.localRot);
                        }

                        if (showHookRotationHandles)
                        {
                            //Rotation handles for hooks require the identity matrix because the rotation handle does not draw properly with rotated matrices
                            Handles.matrix = Matrix4x4.identity;
                            //A special quaternion must be set up because the handle matrix is temporarily in world space
                            Quaternion rot = Handles.RotationHandle(
                                Quaternion.LookRotation(target.transform.TransformDirection(curHook.localRot * Vector3.forward), target.transform.TransformDirection(curHook.localRot * Vector3.up)),
                                m.MultiplyPoint3x4(curHook.localPos));

                            curHook.localRot = Quaternion.LookRotation(target.transform.InverseTransformDirection(rot * Vector3.forward), target.transform.InverseTransformDirection(rot * Vector3.up));
                            Handles.matrix = m;//Reverting to proper matrix for handles

                            if (GUI.changed)
                            {
                                //Setting euler angles from rotation
                                curHook.localEulerRot = curHook.localRot.eulerAngles;
                            }
                        }

                        if (showHookRadiusHandles)
                        {
                            //Hook radius handles
                            Handles.color = Color.magenta;
                            curHook.radius = Mathf.Max(0.01f, Handles.SnapValue(Handles.RadiusHandle(curHook.localRot, curHook.localPos, curHook.radius), 0.0f));
                        }

                        if (showHookStrengthHandles)
                        {
                            //Handles for strength and falloff of hooks
                            Handles.color = Color.magenta;
                            curHook.strength = Handles.ScaleSlider(curHook.strength, curHook.localPos, curHook.localRot * Vector3.forward, curHook.localRot, handleSize * 2.0f, 0.1f);

                            Handles.color = new Color(1.0f, 0.5f, 0.7f, 1.0f);
                            if (Mathf.Abs(curHook.strength) < 0.01f)
                            {
                                curHook.strength = 0.01f * Mathf.Sign(curHook.strength);
                            }

                            if (curHook.strength < 0)
                            {
                                Handles.color = new Color(0.7f, 0.0f, 1.0f, 1.0f);
                            }

                            Handles.DrawLine(curHook.localPos, curHook.localPos + curHook.localRot * Vector3.forward * curHook.radius * curHook.strength);
                            Handles.DrawWireDisc(curHook.localPos + curHook.localRot * Vector3.forward * curHook.radius * curHook.strength, curHook.localRot * Vector3.forward, handleSize * 0.2f);

                            Handles.color = new Color(0.0f, 1.0f, 1.0f, 1.0f);
                            curHook.falloff = Mathf.Max(0.01f, Handles.ScaleSlider(curHook.falloff, curHook.localPos, curHook.localRot * Vector3.up, curHook.localRot, handleSize * 2.0f, 0.1f));
                            Handles.DrawLine(curHook.localPos, curHook.localPos + curHook.localRot * Vector3.up * curHook.radius * curHook.falloff);
                            Handles.DrawWireDisc(curHook.localPos + curHook.localRot * Vector3.up * curHook.radius * curHook.falloff, curHook.localRot * Vector3.up, handleSize * 0.2f);
                        }

                        if (showHookNames)
                        {
                            //Drawing the names of hooks
                            Handles.Label(curHook.localPos, curHook.name, hookLabel);
                        }
                    }
                }
            }
            Handles.matrix = Matrix4x4.identity;

            SceneView.RepaintAll();

            if ((liveEdit || livePreview) && EditorApplication.timeSinceStartup - lastUpdateTime > sceneUpdateTime)
            {
                //Updating of active collider during editing
                lastUpdateTime = EditorApplication.timeSinceStartup;
                GenerateCollider(liveEdit);
            }

            if (GUI.changed && target != null && useColliderGroup && colGroup != null)
            {
                //Let the Unity editor know if changes need to be saved
                EditorUtility.SetDirty(colGroup);
            }
        }

        //Generation of colliders
        void GenerateCollider(bool updateCollider)//If true, the collider component is updated, otherwise only the live preview is updated
        {
            genProps.hooks = hooks;
            ColliderGenerator.ColliderFinishStatus cGen = ColliderGenerator.ColliderFinishStatus.Fail;//Tracks errors in generation
            if (colMeshes[colIndex] != null)
            {
                DestroyImmediate(colMeshes[colIndex]);//Destroying old collision mesh
            }
            colMeshes[colIndex] = ColliderGenerator.GenerateCollider(ref genProps, out cGen, !updateCollider);//The actual mesh generation
            if (cGen != ColliderGenerator.ColliderFinishStatus.Success)
            {
                //Error handling
                if (!errorOccurred && updateCollider)
                {
                    errorOccurred = true;
                    switch (cGen)
                    {
                        case ColliderGenerator.ColliderFinishStatus.Fail:
                            Debug.LogError("Collision mesh generation failed for an unspecified reason.");
                            break;
                        case ColliderGenerator.ColliderFinishStatus.FailTriCount:
                            Debug.LogError("Generated collision mesh has greater than 255 polygons and cannot be created.");
                            break;
                        case ColliderGenerator.ColliderFinishStatus.DetailTimeout:
                            Debug.LogError("Collider generator reached max detail reduction attempts. Generated collision mesh has greater than 255 polygons and cannot be created.");
                            break;
                    }
                }
            }
            else if (colMeshes[colIndex] != null)
            {
                //Successful generation
                colMeshes[colIndex].RecalculateNormals();

                if (updateCollider)
                {
                    errorOccurred = false;
                    SetCollider();
                }
            }
            else if (!errorOccurred && updateCollider)
            {
                //Unlikely case where generated mesh is null
                errorOccurred = true;
                Debug.LogError("Collision mesh reference is null and cannot be used.");
            }
        }

        //Sets up the collider components
        void SetCollider()
        {
            if (useColliderGroup)
            {
                //Setting a collider in a group
                if (colGroup != null)
                {
                    if (colIndex < colGroup.colliders.Count)
                    {
                        //Replacing collider in group
                        colMeshes[colIndex].name = colGroup.colliders[colIndex].name;
                        colGroup.SetCollider(colIndex, Instantiate(colMeshes[colIndex]), genProps);
                    }
                    else
                    {
                        //Adding a new collider to a group
                        string colName = "Collider " + colIndex.ToString();
                        colMeshes[colIndex].name = colName;
                        genProps = new GeneratorProps(genProps);
                        colGroup.AddCollider(new ColliderInstance(colName, Instantiate(colMeshes[colIndex]), genProps));
                    }
                }
            }
            else
            {
                //Setting a collider without a group
                colMeshes[colIndex].name = "CCC Generated Collider";
                RefreshColliderList();//Get list of mesh colliders on target object

                //First check if collider exists that was generated and replace it, otherwise add it to first component with null mesh
                int addIndex = -1;
                for (int i = 0; i < meshCols.Count; i++)
                {
                    if (addIndex == -1)
                    {
                        if (meshCols[i].sharedMesh != null)
                        {
                            if (meshCols[i].sharedMesh.name.StartsWith("CCC Generated"))
                            {
                                //Index of previously generated collider
                                addIndex = i;
                            }
                        }
                        else
                        {
                            addIndex = i;
                        }
                    }
                }

                if (addIndex == -1)
                {
                    //Adding new mesh collider component
                    MeshCollider newCol = Undo.AddComponent<MeshCollider>(target);
                    newCol.sharedMesh = null;
                    newCol.convex = true;
                    newCol.isTrigger = genProps.isTrigger;
                    newCol.sharedMaterial = genProps.physMat;
                    newCol.sharedMesh = Instantiate(colMeshes[colIndex]);
                }
                else
                {
                    //Setting mesh on existing mesh collider component
                    meshCols[addIndex].convex = true;
                    meshCols[addIndex].isTrigger = genProps.isTrigger;
                    meshCols[addIndex].sharedMaterial = genProps.physMat;
                    meshCols[addIndex].sharedMesh = Instantiate(colMeshes[colIndex]);
                }
            }
        }

        //Duplicates the active collider
        void DuplicateCollider()
        {
            if (useColliderGroup)
            {
                if (colGroup != null)
                {
                    colGroup.DuplicateCollider(colIndex);
                }
            }
        }

        //This duplicate function is for the inspector button on collider group components
        public void DuplicateCollider(int index)//Index is the collider index
        {
            if (useColliderGroup)
            {
                if (colGroup != null)
                {
                    colGroup.DuplicateCollider(index);
                }
            }
        }

        //Deletes the active collider
        void DeleteCollider()
        {
            if (useColliderGroup)
            {
                //Deletion for groups
                if (colGroup != null)
                {
                    colGroup.DeleteCollider(colIndex);
                    LimitIndex(colGroup.colliders.Count);
                }
            }
            else
            {
                //Deleted previously generated collider without group
                RefreshColliderList();
                for (int i = 0; i < meshCols.Count; i++)
                {
                    if (meshCols[i].sharedMesh != null)
                    {
                        if (meshCols[i].sharedMesh.name.StartsWith("CCC Generated"))
                        {
                            Undo.DestroyObjectImmediate(meshCols[i]);
                            break;
                        }
                    }
                }
                meshCols.RemoveAll(col => col == null);
            }
        }

        //Deletes all colliders on target object
        void DeleteAllColliders()
        {
            if (useColliderGroup)
            {
                if (colGroup != null)
                {
                    colGroup.DeleteAllColliders();
                }
            }
            else
            {
                RefreshColliderList();
                for (int i = 0; i < meshCols.Count; i++)
                {
                    Undo.DestroyObjectImmediate(meshCols[i]);
                }
                meshCols.RemoveAll(col => col == null);
            }
        }

        //Gets list of mesh collider components on target object
        void RefreshColliderList()
        {
            if (target == null)
            {
                return;
            }
            target.GetComponents(meshCols);
        }

        //Gets the current collider properties that should be modified
        void UpdateColliderFromIndex(int maxIndex)
        {
            if (colIndex == maxIndex)
            {
                genProps = genPropsTemp;//Temporary properties for new colliders
            }
            else if (useColliderGroup && colGroup != null)
            {
                genProps = colGroup.colliders[Mathf.Clamp(colIndex, 0, colGroup.colliders.Count)].props;//Properties of existing collider
            }
            else
            {
                genProps = genPropsTemp;//Properties for collider without group
            }

            LimitMaxRadiusProps();
            SetHooksFromProps();
        }

        //Sets the maximum radius and radius offset for handles to not be less than those of the newly selected collider
        void LimitMaxRadiusProps()
        {
            for (int i = 0; i < genProps.corners.Length; i++)
            {
                ColliderCorner curCorner = genProps.corners[i];
                maxCornerRadius = Mathf.Max(maxCornerRadius, curCorner.axleRadii.x, curCorner.axleRadii.y, curCorner.axleRadii.z);
                maxCornerOffset = Mathf.Max(maxCornerOffset, curCorner.radiusOffsets.x, curCorner.radiusOffsets.y, curCorner.radiusOffsets.z);
            }
        }

        //Saves the generated mesh of the active collider as an asset
        void SaveMeshAsset(bool setPath)
        {
            string meshName = "Collider Mesh";
            Mesh savedMesh = null;
            bool cancelled = false;

            //Creating copy of the mesh in order to save it
            if (useColliderGroup && colGroup != null && colIndex < colGroup.colliders.Count)
            {
                if (colGroup.colliders[colIndex].colMesh != null)
                {
                    savedMesh = Instantiate(colGroup.colliders[colIndex].colMesh);
                    meshName = colGroup.colliders[colIndex].name;
                }
            }
            else if (colMeshes[colIndex] != null)
            {
                savedMesh = Instantiate(colMeshes[colIndex]);
                if (!string.IsNullOrEmpty(colMeshes[colIndex].name))
                {
                    meshName = colMeshes[colIndex].name;
                }
            }

            if (savedMesh == null)
            {
                Debug.LogWarning("No mesh found for saving.");
                return;
            }

            if (!genProps.meshAssetPath.EndsWith(".asset") || !AssetDatabase.IsValidFolder(genProps.meshAssetPath.Remove(genProps.meshAssetPath.LastIndexOf('/'))) || setPath)
            {
                //Getting proper save path
                string meshPath = EditorUtility.SaveFilePanelInProject("Save Mesh", meshName, "asset", "Enter a name for the mesh asset to be saved.", genProps.meshAssetPath);
                EditorUtility.FocusProjectWindow();

                cancelled = string.IsNullOrEmpty(meshPath);

                if (!cancelled && meshPath.StartsWith("Assets/"))
                {
                    genProps.meshAssetPath = meshPath;
                }
            }

            if (!cancelled)
            {
                if (genProps.meshAssetPath.EndsWith(".asset") && AssetDatabase.IsValidFolder(genProps.meshAssetPath.Remove(genProps.meshAssetPath.LastIndexOf('/'))))
                {
                    //Actual saving of mesh
                    AssetDatabase.CreateAsset(savedMesh, genProps.meshAssetPath);
                    AssetDatabase.SaveAssets();
                }
                else
                {
                    Debug.LogWarning("Saving mesh failed due to invalid path.");
                }
            }
        }

        //Generates the preview meshes for all colliders
        void RebuildPreviewMeshes(int maxIndex)
        {
            //Adjusting size of preview mesh list
            if (colMeshes.Count < maxIndex + 1)
            {
                colMeshes.AddRange(new Mesh[maxIndex + 1 - colMeshes.Count]);
            }
            else if (colMeshes.Count > maxIndex + 1)
            {
                colMeshes.RemoveRange(maxIndex + 1, colMeshes.Count - (maxIndex + 1));
            }

            if (useColliderGroup && colGroup != null)
            {
                if (colMeshes[maxIndex] != null)
                {
                    DestroyImmediate(colMeshes[maxIndex]);//Destroying old mesh
                }
                colMeshes[maxIndex] = null;

                for (int i = 0; i < maxIndex; i++)
                {
                    if (colMeshes[i] != null)
                    {
                        DestroyImmediate(colMeshes[i]);//Destroying old meshes
                    }

                    if (colGroup.colliders.Count > i)
                    {
                        if (colGroup.colliders[i] != null)
                        {
                            colMeshes[i] = ColliderGenerator.GenerateCollider(ref colGroup.colliders[i].props);//Actual generation
                            if (colMeshes[i] != null)
                            {
                                colMeshes[i].RecalculateNormals();
                            }
                        }
                    }
                }
            }
        }

        //Draws gizmos for approximating a collider
        void DrawColliderApproximation(GeneratorProps gp, bool solidSides)
        {
            ColliderCorner ftr = gp.GetCornerAtLocation(ColliderCorner.CornerId.FrontTopRight);
            ColliderCorner ftl = gp.GetCornerAtLocation(ColliderCorner.CornerId.FrontTopLeft);
            ColliderCorner fbr = gp.GetCornerAtLocation(ColliderCorner.CornerId.FrontBottomRight);
            ColliderCorner fbl = gp.GetCornerAtLocation(ColliderCorner.CornerId.FrontBottomLeft);
            ColliderCorner btr = gp.GetCornerAtLocation(ColliderCorner.CornerId.BackTopRight);
            ColliderCorner btl = gp.GetCornerAtLocation(ColliderCorner.CornerId.BackTopLeft);
            ColliderCorner bbr = gp.GetCornerAtLocation(ColliderCorner.CornerId.BackBottomRight);
            ColliderCorner bbl = gp.GetCornerAtLocation(ColliderCorner.CornerId.BackBottomLeft);
            Color sideColor = new Color(0.5f, 1.0f, 0.5f, 0.1f);
            Color edgeColor = new Color(0.2f, 0.5f, 0.5f, 0.4f);
            Color radiusColor = new Color(0.5f, 1.0f, 0.5f);

            if (!solidSides)
            {
                edgeColor = radiusColor;
                edgeColor.a = 0.7f;
                sideColor.a = 0.0f;
            }

            //Collider side drawing
            Handles.DrawSolidRectangleWithOutline(
                new Vector3[] { ftr.GetEdgePos(Vector3.forward), ftl.GetEdgePos(Vector3.forward), fbl.GetEdgePos(Vector3.forward), fbr.GetEdgePos(Vector3.forward) },
                sideColor, edgeColor);

            Handles.DrawSolidRectangleWithOutline(
                new Vector3[] { ftr.GetEdgePos(Vector3.right), fbr.GetEdgePos(Vector3.right), bbr.GetEdgePos(Vector3.right), btr.GetEdgePos(Vector3.right) },
                sideColor, edgeColor);

            Handles.DrawSolidRectangleWithOutline(
                new Vector3[] { ftr.GetEdgePos(Vector3.up), ftl.GetEdgePos(Vector3.up), btl.GetEdgePos(Vector3.up), btr.GetEdgePos(Vector3.up) },
                sideColor, edgeColor);

            Handles.DrawSolidRectangleWithOutline(
                new Vector3[] { btr.GetEdgePos(Vector3.back), btl.GetEdgePos(Vector3.back), bbl.GetEdgePos(Vector3.back), bbr.GetEdgePos(Vector3.back) },
                sideColor, edgeColor);

            Handles.DrawSolidRectangleWithOutline(
                new Vector3[] { ftl.GetEdgePos(Vector3.left), fbl.GetEdgePos(Vector3.left), bbl.GetEdgePos(Vector3.left), btl.GetEdgePos(Vector3.left) },
                sideColor, edgeColor);

            Handles.DrawSolidRectangleWithOutline(
                new Vector3[] { fbr.GetEdgePos(Vector3.down), fbl.GetEdgePos(Vector3.down), bbl.GetEdgePos(Vector3.down), bbr.GetEdgePos(Vector3.down) },
                sideColor, edgeColor);

            //Collider corner drawing
            Handles.color = radiusColor;
            for (int i = 0; i < gp.corners.Length; i++)
            {
                ColliderCorner curCorner = gp.corners[i];
                float maxRadius = Mathf.Max(curCorner.axleRadii.x, curCorner.axleRadii.y, curCorner.axleRadii.z);
                Vector3 cornerPos = curCorner.localPos - curCorner.GetOffset();
                Handles.DrawWireDisc(cornerPos, Vector3.right, maxRadius);
                Handles.DrawWireDisc(cornerPos, Vector3.up, maxRadius);
                Handles.DrawWireDisc(cornerPos, Vector3.forward, maxRadius);
            }
        }
    }
}
#endif