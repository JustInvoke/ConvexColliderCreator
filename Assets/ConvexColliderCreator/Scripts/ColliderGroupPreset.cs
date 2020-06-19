// Copyright (c) 2018 Justin Couch / JustInvoke
using System.Collections.Generic;
using UnityEngine;

namespace ConvexColliderCreator
{
    [CreateAssetMenu(fileName = "Collider Group Preset", menuName = "Convex Collider Creator/Collider Group Preset", order = 1)]
    //Class for asset files that contain presets for collider groups
    public class ColliderGroupPreset : ScriptableObject
    {
        public List<ColliderInstance> colliders = new List<ColliderInstance>();//Colliders in the group

        //Clears all colliders in the preset
        public void ClearColliders()
        {
            for (int i = 0; i < colliders.Count; i++)
            {
                colliders[i].DestroyMesh();
            }
            colliders.Clear();
        }
    }
}
