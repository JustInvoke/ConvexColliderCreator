// Copyright (c) 2018 Justin Couch / JustInvoke
using UnityEngine;

namespace ConvexColliderCreator
{
    [System.Serializable]
    //Class for a "hook" that can push/pull, twist, or expand/shrink part of a mesh to deform it
    public class DeformHook
    {
        public string name = "Hook";//Name of the hook for identification
        public bool enabled = true;//Whether the hook is actively deforming a mesh
        public bool showHandles = true;//Whether to show handles in the editor
        public enum HookType { Pull, Twist, Expand } //Possible deformation types
        public HookType hookType = HookType.Pull;//Current deformation type
        public Vector3 localPos = Vector3.zero;//Local position
        public Quaternion localRot = Quaternion.identity;//Local rotation
        public Vector3 localEulerRot = Quaternion.identity.eulerAngles;//Euler representation of local rotation
        public float radius = 0.5f;//Radius of vertices affected
        public float strength = 1.0f;//Strength of deformation
        public float falloff = 1.0f;//Falloff for deformation based on distance
        public bool set = false;//Whether the hook properties have been set in the editor

        public DeformHook() { } //Empty constructor with default values

        //Creates a new hook with the given properties
        public DeformHook(string n, HookType ht, Vector3 pos, Quaternion rot, float r, float s, float f)
        {
            name = n;
            enabled = true;
            hookType = ht;
            localPos = pos;
            localRot = rot;
            radius = r;
            strength = s;
            falloff = f;
            set = true;
        }

        //Creates a new hook by copying an existing one
        public DeformHook(DeformHook h)
        {
            name = h.name;
            enabled = h.enabled;
            showHandles = h.showHandles;
            hookType = h.hookType;
            localPos = h.localPos;
            localRot = h.localRot;
            localEulerRot = h.localEulerRot;
            radius = h.radius;
            strength = h.strength;
            falloff = h.falloff;
            set = true;
        }

        //Resets properties to default values
        public void Reset()
        {
            localPos = Vector3.zero;
            localRot = Quaternion.identity;
            localEulerRot = localRot.eulerAngles;
            radius = 0.5f;
            strength = 1.0f;
            falloff = 1.0f;
        }

        //Sets up initial values in editor window
        public void Initialize()
        {
            Reset();
            name = "Hook";
            enabled = true;
            showHandles = true;
            localPos = Vector3.forward * 0.5f;//New hooks are offset from the origin so their position handles don't overlap those of the target gameobject's
            set = true;
        }

        //Sets local quaternion rotation from the local Euler rotation
        public void SetRotationFromEuler()
        {
            localRot = Quaternion.Euler(localEulerRot.x, localEulerRot.y, localEulerRot.z);
        }

        //Sets the rotation of the hook from the quaternion
        public void SetRotation(Quaternion rot)
        {
            localRot = rot;
            localEulerRot = rot.eulerAngles;
        }

        //Sets the rotation of the hook from the Vector3 Euler rotation
        public void SetRotation(Vector3 dir)
        {
            localEulerRot = dir;
            SetRotationFromEuler();
        }
    }
}
