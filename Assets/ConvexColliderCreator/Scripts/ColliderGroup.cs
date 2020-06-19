// Copyright (c) 2018 Justin Couch / JustInvoke
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ConvexColliderCreator
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Convex Collider Group")]
    //Class for collider groups attached to game objects
    public class ColliderGroup : MonoBehaviour
    {
        public bool generateOnStart = false;//Whether to generate all colliders in Start()
        public ColliderGroupPreset linkedPreset;//Preset for saving and loading colliders
        public bool showPresetOptions = true;//Whether to show preset options in the inspector
        public bool showColliderList = true;//Whether to show the list of colliders in the inspector
        public List<MeshCollider> meshColComponents = new List<MeshCollider>();//List of mesh colliders on object
        public List<ColliderInstance> colliders = new List<ColliderInstance>();//List of collider property containers on object
        [System.NonSerialized]
        public bool loadUpdated = false;//True if a preset has been loaded and mesh previews need to be updated
        [System.NonSerialized]
        public bool generating = false;//True if asynchronous collider generation is in progress

        private void Start()
        {
            if (generateOnStart)
            {
                GenerateAllColliders();
            }
        }

        //Adds a new collider to the group with default properties
        public ColliderInstance AddCollider()
        {
            ColliderInstance newCol = new ColliderInstance("Collider " + colliders.Count.ToString());
            colliders.Add(newCol);
            SetColliderComponents(false);
            return newCol;
        }

        //Adds a new collider to the group with the given properties
        public ColliderInstance AddCollider(GeneratorProps gp)
        {
            ColliderInstance newCol = new ColliderInstance(gp);
            colliders.Add(newCol);
            SetColliderComponents(false);
            return newCol;
        }

        //Adds an existing collider to the group
        public void AddCollider(ColliderInstance ci)
        {
            colliders.Add(ci);
            SetColliderComponents(false);
        }

        //Sets the collider at the index in the list to use the given mesh and properties
        public void SetCollider(int index, Mesh m, GeneratorProps gp)
        {
            if (m != null)
            {
                colliders[index].colMesh = Instantiate(m);
            }

            if (gp != null)
            {
                colliders[index].props = gp;
            }

            SetColliderComponents(true);
        }

        //Duplicates the collider at the index in the list
        public void DuplicateCollider(int index)
        {
            if (index < 0 || index > colliders.Count)
            {
                Debug.LogWarning("Duplication failed because collider at index " + index.ToString() + " does not exist.");
                return;
            }

            colliders.Insert(index + 1, new ColliderInstance(
                colliders[index].name + " Copy",
                colliders[index].colMesh,
                new GeneratorProps(colliders[index].props)));

            SetColliderComponents(false);
        }

        //Deletes the collider at the index in the list
        public void DeleteCollider(int index)
        {
            if (index < 0 || index >= colliders.Count)
            {
                Debug.LogWarning("Deletion failed because collider at index " + index.ToString() + " does not exist.");
                return;
            }

            colliders.RemoveAt(index);
            SetColliderComponents(true);
        }

        //Deletes a specified collider in the group if it exists
        public void DeleteCollider(ColliderInstance ci)
        {
            if (colliders.Contains(ci))
            {
                colliders.Remove(ci);
                SetColliderComponents(true);
            }
            else
            {
                Debug.LogWarning("Deletion failed because the collider does not exist in the colliders list.");
            }
        }

        //Deletes all colliders in the group
        public void DeleteAllColliders()
        {
            ClearColliders();
            SetColliderComponents(true);
        }

        //Destroys the collider group component
        public void DestroyComponent()
        {
            ClearColliders();
            SetColliderComponents(false);
            if (Application.isPlaying)
            {
                Destroy(this);
            }
            else
            {
                DestroyImmediate(this);
#if UNITY_EDITOR
                GUIUtility.ExitGUI();//Prevents Unity from throwing errors with inspector drawing
#endif
            }
        }

        //Clears the list of colliders but doesn't remove mesh collider components
        public void ClearColliders()
        {
            for (int i = 0; i < colliders.Count; i++)
            {
                colliders[i].DestroyMesh();
            }
            colliders.Clear();
        }

        //Generates a collision mesh from the collider at the index in the list
        public void GenerateCollider(int index)
        {
            if (index < 0 || index >= colliders.Count)
            {
                Debug.LogWarning("Generation failed because collider at index " + index.ToString() + " does not exist.");
                return;
            }

            colliders[index].GenerateCollider();
            SetColliderComponents(true);
        }

        //Generates collision meshes for all colliders in the group
        public void GenerateAllColliders()
        {
            if (generating)
            {
                Debug.LogWarning("Colider generation already in progress.");
                return;
            }

            for (int i = 0; i < colliders.Count; i++)
            {
                colliders[i].GenerateCollider();
            }
            SetColliderComponents(true);
        }

        //Asynchronous generation method that uses a coroutine to stagger the generation of each collider
        //If async is true, each collider will be generated during a separate frame
        public void GenerateAllColliders(bool async)
        {
            if (generating)
            {
                Debug.LogWarning("Colider generation already in progress.");
                return;
            }

            if (async)
            {
                StopAllCoroutines();
                StartCoroutine(GenerateAllCollidersAsync());
            }
            else
            {
                GenerateAllColliders();
            }
        }

        //Coroutine for asynchronous collider generation, one collider is generated each frame
        IEnumerator GenerateAllCollidersAsync()
        {
            generating = true;
            for (int i = 0; i < colliders.Count; i++)
            {
                colliders[i].GenerateCollider();
                yield return null;
            }
            SetColliderComponents(true);
            generating = false;
        }

        //Sets up mesh collider components with proper collision meshes that have been generated
        //exitGUI is for preventing Unity from throwing errors with inspector drawing
        public void SetColliderComponents(bool exitGUI)
        {
            GetComponents(meshColComponents);
            int countDiff = colliders.Count - meshColComponents.Count;
            if (countDiff > 0)
            {
                //Add new mesh collider components if necessary
                while (countDiff > 0)
                {
                    MeshCollider newCol = gameObject.AddComponent<MeshCollider>();
                    newCol.sharedMesh = null;
                    newCol.convex = true;
                    meshColComponents.Add(newCol);
                    countDiff--;
                }
            }
            else if (countDiff < 0)
            {
                //Destroy extra mesh collider components if necessary
                while (countDiff < 0)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(meshColComponents[meshColComponents.Count + countDiff]);
                    }
                    else
                    {
                        DestroyImmediate(meshColComponents[meshColComponents.Count + countDiff]);
                    }
                    countDiff++;
                }
                meshColComponents.RemoveAll(col => col == null);
            }

            //Set mesh collider properties
            for (int i = 0; i < colliders.Count; i++)
            {
                meshColComponents[i].isTrigger = colliders[i].props.isTrigger;
                meshColComponents[i].sharedMaterial = colliders[i].props.physMat;
                meshColComponents[i].sharedMesh = colliders[i].colMesh;
            }

#if UNITY_EDITOR
            if (exitGUI && !Application.isPlaying)
            {
                GUIUtility.ExitGUI();//In case of null references in Unity inpsector drawing due to destroyed components
            }
#endif
        }

        //Saves colliders to group preset and returns true if successful
        public bool SaveToPreset()
        {
            if (linkedPreset != null)
            {
                if (linkedPreset.colliders == null)
                {
                    linkedPreset.colliders = new List<ColliderInstance>();
                }
                linkedPreset.ClearColliders();

                for (int i = 0; i < colliders.Count; i++)
                {
                    linkedPreset.colliders.Add(new ColliderInstance(colliders[i]));
                }

#if UNITY_EDITOR
                if (Application.isEditor)
                {
                    //Let Unity know to save the preset
                    UnityEditor.EditorUtility.SetDirty(linkedPreset);
                    UnityEditor.AssetDatabase.SaveAssets();
                }
#endif
                return true;
            }
            else
            {
                Debug.LogWarning("No linked preset to save to.");
                return false;
            }
        }

        //Loads colliders from group preset and returns true if successful
        public bool LoadFromPreset()
        {
            if (linkedPreset != null)
            {
                if (linkedPreset.colliders != null)
                {
                    loadUpdated = true;
                    ClearColliders();
                    for (int i = 0; i < linkedPreset.colliders.Count; i++)
                    {
                        colliders.Add(new ColliderInstance(linkedPreset.colliders[i]));
                    }
                    SetColliderComponents(true);
                    return true;
                }
                else
                {
                    Debug.LogWarning("Linked preset is missing colliders list to load from.");
                    return false;
                }
            }
            else
            {
                Debug.LogWarning("No linked preset to load from.");
                return false;
            }
        }
    }

    [System.Serializable]
    //Class for individual colliders
    public class ColliderInstance
    {
        public string name = "Collider";//Name of collider for identification
        public Mesh colMesh;//The actual collision mesh
        public GeneratorProps props;//The properties of the collider for mesh generation

        //Creates a new collider with default properties
        public ColliderInstance()
        {
            props = new GeneratorProps(Vector3.one, 0.1f);
        }

        //Creates a new collider with the given name and default properties
        public ColliderInstance(string n)
        {
            name = n;
            props = new GeneratorProps(Vector3.one, 0.1f);
        }

        //Creates a new collider with the given properties
        public ColliderInstance(GeneratorProps gp)
        {
            if (gp != null)
            {
                props = gp;
            }
            else
            {
                props = new GeneratorProps(Vector3.one, 0.1f);
            }
        }

        //Creates a new collider with the given name and properties
        public ColliderInstance(string n, GeneratorProps gp)
        {
            name = n;

            if (gp != null)
            {
                props = gp;
            }
            else
            {
                props = new GeneratorProps(Vector3.one, 0.1f);
            }
        }

        //Creates a new collider with the given name, mesh, and properties
        public ColliderInstance(string n, Mesh m, GeneratorProps gp)
        {
            name = n;
            if (m != null)
            {
                colMesh = Object.Instantiate(m);
            }

            if (gp != null)
            {
                props = gp;
            }
            else
            {
                props = new GeneratorProps(Vector3.one, 0.1f);
            }
        }

        //Creates a new collider by copying an existing one
        public ColliderInstance(ColliderInstance ci)
        {
            name = ci.name;
            if (ci.colMesh != null)
            {
                colMesh = Object.Instantiate(ci.colMesh);
            }

            props = new GeneratorProps(ci.props);
        }

        //Generates the collision mesh for this collider
        public void GenerateCollider()
        {
            ColliderGenerator.ColliderFinishStatus cGen = ColliderGenerator.ColliderFinishStatus.Fail;
            DestroyMesh();//Destroy old mesh
            Mesh newMesh = ColliderGenerator.GenerateCollider(ref props, out cGen);//Actual mesh generation
            if (cGen != ColliderGenerator.ColliderFinishStatus.Success)
            {
                //Error handling
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

                if (Application.isPlaying)
                {
                    Object.Destroy(newMesh);
                }
                else
                {
                    Object.DestroyImmediate(newMesh);
                }
            }
            else if (newMesh != null)
            {
                //Setting of mesh after successful generation
                colMesh = newMesh;
                colMesh.name = name;

                if (Application.isEditor)
                {
                    colMesh.RecalculateNormals();
                }
            }
            else
            {
                //Unlikely case where generated mesh is null
                Debug.LogError("Collision mesh reference is null and cannot be used.");

                if (Application.isPlaying)
                {
                    Object.Destroy(newMesh);
                }
                else
                {
                    Object.DestroyImmediate(newMesh);
                }
            }
        }

        //Destroys the mesh associated with this collider and returns true if successful
        public bool DestroyMesh()
        {
            if (colMesh != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(colMesh);
                    return true;
                }
                else
                {
                    Object.DestroyImmediate(colMesh);
                    return true;
                }
            }
            return false;
        }
    }
}
