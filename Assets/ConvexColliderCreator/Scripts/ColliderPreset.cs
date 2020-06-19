// Copyright (c) 2018 Justin Couch / JustInvoke
using UnityEngine;

namespace ConvexColliderCreator
{
    [CreateAssetMenu(fileName = "Collider Preset", menuName = "Convex Collider Creator/Collider Preset", order = 0)]
    //Class for asset files that contain presets for colliders
    public class ColliderPreset : ScriptableObject
    {
        public GeneratorProps props;//Collider properties
    }
}
