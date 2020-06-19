// Copyright (c) 2018 Justin Couch / JustInvoke
using System;
using UnityEngine;

namespace ConvexColliderCreator
{
    [Serializable]
    //Class for individual corners of colliders
    public class ColliderCorner
    {
        //Location identifier for each corner
        public enum CornerId { FrontTopRight, FrontTopLeft, FrontBottomRight, FrontBottomLeft, BackTopRight, BackTopLeft, BackBottomRight, BackBottomLeft }
        public CornerId cornerLocation;//Current corner location; do not assign directly
        public Vector3 normalizedCornerLocation = Vector3.zero;//Vector3 representing corner location; do not assign directly
        public Vector3 localPos = Vector3.zero;//Local position of the corner
        public Vector3 axleRadii = Vector3.one;//Component-wise radii of the corner
        public Vector3 radiusOffsets = Vector3.one;//Component-wise radius offsets
        //Radius offsets control how the local "center" of the corner changes as the radii do during mesh generation

        public ColliderCorner() { } //Empty constructor

        //Creates a new collider corner by copying an existing one
        public ColliderCorner(ColliderCorner cc)
        {
            cornerLocation = cc.cornerLocation;
            normalizedCornerLocation = cc.normalizedCornerLocation;
            localPos = cc.localPos;
            axleRadii = cc.axleRadii;
            radiusOffsets = cc.radiusOffsets;
        }

        //Resets the corner radii and radius offsets to their default values
        public void ResetRadii()
        {
            axleRadii = Vector3.one * 0.1f;
            radiusOffsets = Vector3.one;
        }

        //Sets the corner location based on its local position; does not work if local position is zero on any axis
        public void SetCornerLocationFromPosition()
        {
            if (Math.Sign(localPos.x) == 0 || Math.Sign(localPos.y) == 0 || Math.Sign(localPos.z) == 0)
            {
                Debug.LogWarning("Corner location cannot be identified because at least one component of the local position is zero.");
                return;
            }

            //Basic nested switch statements are satisfactory, using the sign of the local position on each axis
            switch ((int)Mathf.Sign(localPos.z))
            {
                case 1:
                    switch ((int)Mathf.Sign(localPos.y))
                    {
                        case 1:
                            switch ((int)Mathf.Sign(localPos.x))
                            {
                                case 1:
                                    cornerLocation = CornerId.FrontTopRight;
                                    normalizedCornerLocation = Vector3.one;
                                    break;
                                case -1:
                                    cornerLocation = CornerId.FrontTopLeft;
                                    normalizedCornerLocation.Set(-1.0f, 1.0f, 1.0f);
                                    break;
                            }
                            break;
                        case -1:
                            switch ((int)Mathf.Sign(localPos.x))
                            {
                                case 1:
                                    cornerLocation = CornerId.FrontBottomRight;
                                    normalizedCornerLocation.Set(1.0f, -1.0f, 1.0f);
                                    break;
                                case -1:
                                    cornerLocation = CornerId.FrontBottomLeft;
                                    normalizedCornerLocation.Set(-1.0f, -1.0f, 1.0f);
                                    break;
                            }
                            break;
                    }
                    break;
                case -1:
                    switch ((int)Mathf.Sign(localPos.y))
                    {
                        case 1:
                            switch ((int)Mathf.Sign(localPos.x))
                            {
                                case 1:
                                    cornerLocation = CornerId.BackTopRight;
                                    normalizedCornerLocation.Set(1.0f, 1.0f, -1.0f);
                                    break;
                                case -1:
                                    cornerLocation = CornerId.BackTopLeft;
                                    normalizedCornerLocation.Set(-1.0f, 1.0f, -1.0f);
                                    break;
                            }
                            break;
                        case -1:
                            switch ((int)Mathf.Sign(localPos.x))
                            {
                                case 1:
                                    cornerLocation = CornerId.BackBottomRight;
                                    normalizedCornerLocation.Set(1.0f, -1.0f, -1.0f);
                                    break;
                                case -1:
                                    cornerLocation = CornerId.BackBottomLeft;
                                    normalizedCornerLocation = -Vector3.one;
                                    break;
                            }
                            break;
                    }
                    break;
            }
        }

        //Gets the amount the center of the corner should be offset based on the radii and radius offsets
        public Vector3 GetOffset()
        {
            return Vector3.Scale(axleRadii, Vector3.Scale(normalizedCornerLocation, radiusOffsets));
        }

        //Gets the point on an edge of the corner in local space in the given direction based on the radii and radius offsets
        public Vector3 GetEdgePos(Vector3 dir)
        {
            return localPos - GetOffset() + Vector3.Scale(axleRadii, dir.normalized);
        }

        //Sets either the radii or radius offsets from setProp and clamped by maxOffset
        //propToSet: True = axle radii, false = radius offsets
        public void SetRadiusProperty(Vector3 setProp, bool propToSet, float maxOffset)
        {
            if (propToSet)
            {
                axleRadii = new Vector3(
                    Mathf.Clamp(setProp.x, 0.0f, maxOffset),
                    Mathf.Clamp(setProp.y, 0.0f, maxOffset),
                    Mathf.Clamp(setProp.z, 0.0f, maxOffset));
            }
            else
            {
                radiusOffsets = new Vector3(
                    Mathf.Clamp(setProp.x, 0.0f, maxOffset),
                    Mathf.Clamp(setProp.y, 0.0f, maxOffset),
                    Mathf.Clamp(setProp.z, 0.0f, maxOffset));
            }
        }
    }
}