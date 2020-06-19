// Copyright (c) 2018 Justin Couch / JustInvoke
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace ConvexColliderCreator
{
    //Inspector editor for collider groups
    [CustomEditor(typeof(ColliderGroup))]
    public class ColliderGroupEditor : Editor
    {
        ColliderGroup targetGroup;//Selected collider group
        static bool drawDefaultInspector = false;//Whether to draw the default inspector with visible variables

        private void OnEnable()
        {
            targetGroup = (ColliderGroup)serializedObject.targetObject;
        }

        public override void OnInspectorGUI()
        {
            GUIStyle boldFoldout = new GUIStyle(EditorStyles.foldout);
            boldFoldout.fontStyle = FontStyle.Bold;

            if (!serializedObject.isEditingMultipleObjects && targetGroup != null)
            {
                Undo.RecordObject(targetGroup, "Collider Group Inspector Change");
                bool isPrefab = Selection.assetGUIDs.Length > 0 && PrefabUtility.GetPrefabAssetType(targetGroup) != PrefabAssetType.NotAPrefab && PrefabUtility.GetPrefabAssetType(targetGroup) != PrefabAssetType.MissingAsset;

                //GUI styling
                GUILayoutOption guiWidthField = GUILayout.Width(200f);
                GUILayoutOption guiWidthButton = GUILayout.Width(80f);
                GUILayoutOption guiWidthButton2 = GUILayout.Width(150f);

                GUIStyle noticeText = new GUIStyle(GUI.skin.label);
                noticeText.fontStyle = FontStyle.BoldAndItalic;

                GUIStyle primaryButton = new GUIStyle(GUI.skin.button);
                primaryButton.fontSize = 12;
                primaryButton.fontStyle = FontStyle.Bold;
                primaryButton.fixedHeight = EditorGUIUtility.singleLineHeight * 2.0f;

                GUIStyle secondaryButton = new GUIStyle(EditorStyles.toolbarButton);
                secondaryButton.fontSize = 12;
                secondaryButton.fontStyle = FontStyle.Bold;
                secondaryButton.fixedHeight = EditorGUIUtility.singleLineHeight * 1.5f;

                targetGroup.generateOnStart = EditorGUILayout.Toggle("Generate Colliders on Start", targetGroup.generateOnStart);

                //Linked preset options
                targetGroup.showPresetOptions = EditorGUILayout.Foldout(targetGroup.showPresetOptions, "Preset Options", boldFoldout);
                if (targetGroup.showPresetOptions)
                {
                    EditorGUI.indentLevel++;
                    targetGroup.linkedPreset = (ColliderGroupPreset)EditorGUILayout.ObjectField("Preset", targetGroup.linkedPreset, typeof(ColliderGroupPreset), true);
                    EditorGUILayout.Space();

                    GUILayout.BeginHorizontal();
                    EditorGUI.BeginDisabledGroup(targetGroup.linkedPreset == null);
                    if (GUILayout.Button("Save To Preset", secondaryButton, guiWidthButton2))
                    {
                        targetGroup.SaveToPreset();
                    }

                    if (GUILayout.Button("Load From Preset", secondaryButton, guiWidthButton2))
                    {
                        targetGroup.LoadFromPreset();
                    }
                    EditorGUI.EndDisabledGroup();
                    GUILayout.EndHorizontal();
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

                targetGroup.showColliderList = EditorGUILayout.Foldout(targetGroup.showColliderList, "Colliders", boldFoldout);

                EditorGUI.BeginDisabledGroup(isPrefab);//Disable editing if the collider group is on a prefab
                //Collider list options
                if (targetGroup.showColliderList)
                {
                    if (isPrefab)
                    {
                        EditorGUILayout.LabelField("Place prefab in scene or open in prefab stage to edit colliders.", noticeText);
                    }

                    ColliderGeneratorWindow win;//The collider editor window
                    EditorGUI.indentLevel++;
                    //Loop through all colliders in group and draw GUI for them
                    for (int i = 0; i < targetGroup.colliders.Count; i++)
                    {
                        EditorGUILayout.Space();
                        ColliderInstance curCol = targetGroup.colliders[i];

                        GUILayout.BeginHorizontal();
                        curCol.name = EditorGUILayout.TextField(curCol.name, guiWidthField);
                        GUILayout.Space(10f);

                        if (GUILayout.Button("Edit", guiWidthButton))
                        {
                            //Open window to edit specific collider
                            GetWindow(out win);
                            win.SetTargetFromGroup(new ColliderGroupTargetInfo(targetGroup.gameObject, targetGroup, i));
                        }
                        GUILayout.Space(10f);

                        if (GUILayout.Button("Duplicate", guiWidthButton))
                        {
                            //Duplicate a collider
                            targetGroup.DuplicateCollider(i);
                        }

                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Delete", guiWidthButton))
                        {
                            //Delete a collider
                            targetGroup.DeleteCollider(i);

                            if (ColliderGeneratorWindow.WindowIsOpen())
                            {
                                GetWindow(out win);
                                win.LimitIndex(targetGroup.colliders.Count);
                            }

                            if (!Application.isPlaying)
                            {
                                GUIUtility.ExitGUI();
                            }
                        }
                        GUILayout.EndHorizontal();
                    }

                    EditorGUILayout.Space();
                    if (GUILayout.Button("Create New", guiWidthField))
                    {
                        //Add a new collider
                        targetGroup.AddCollider();
                        GetWindow(out win);
                        win.SetTargetFromGroup(new ColliderGroupTargetInfo(targetGroup.gameObject, targetGroup, targetGroup.colliders.Count - 1));
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

                EditorGUI.BeginDisabledGroup(targetGroup.colliders.Count == 0);
                if (GUILayout.Button("Generate Colliders", primaryButton))
                {
                    //Generate all of the colliders on the group
                    targetGroup.GenerateAllColliders();
                }
                EditorGUI.EndDisabledGroup();
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            drawDefaultInspector = EditorGUILayout.Foldout(drawDefaultInspector, "Draw Default Inspector", boldFoldout);

            if (drawDefaultInspector)
            {
                //Draw the default inspector in order to see collider properties explicitly
                EditorGUI.indentLevel++;
                DrawDefaultInspector();
                EditorGUI.indentLevel--;
            }

            if (GUI.changed && targetGroup != null)
            {
                //Save properties if modified
                EditorUtility.SetDirty(targetGroup);
            }
        }

        void GetWindow(out ColliderGeneratorWindow cWin)
        {
            //Open collider editor window
            cWin = EditorWindow.GetWindow<ColliderGeneratorWindow>(false, "Convex Collider Creator", true);
        }
    }

    //Struct for passing collider selection information to editor window
    public struct ColliderGroupTargetInfo
    {
        //Creates a new info struct with the given target game object, target collider group, and index in the collider list
        public ColliderGroupTargetInfo(GameObject t, ColliderGroup cg, int i)
        {
            target = t;
            group = cg;
            index = i;
        }

        public GameObject target;//Selected object
        public ColliderGroup group;//Selected collider group
        public int index;//Index of the collider in the list of colliders
    }
}
#endif