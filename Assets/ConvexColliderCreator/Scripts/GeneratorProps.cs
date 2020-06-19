// Copyright (c) 2018 Justin Couch / JustInvoke
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace ConvexColliderCreator
{
    [System.Serializable]
    //Class for storing/passing collider properties
    public class GeneratorProps
    {
        public ColliderPreset linkedPreset;//Preset for saving and loading properties
        public string meshAssetPath = "Assets";//Path in project folder to save a collision mesh to

        public bool isTrigger = false;
        public PhysicMaterial physMat;

        //Modes for modifying the positions of corners in the editor
        public enum CornerPositionEditMode { BoxCenter, BoxSides, Individual }
        public CornerPositionEditMode cornerPositionMode = CornerPositionEditMode.BoxCenter;

        //Modes for modifying the radii and radius offsets of of corners in the editor
        public enum CornerRadiusEditMode { Uniform, UniformIndividual, Advanced }
        public CornerRadiusEditMode cornerRadiusMode = CornerRadiusEditMode.UniformIndividual;
        public CornerRadiusEditMode cornerOffsetMode = CornerRadiusEditMode.UniformIndividual;

        //Modes for modifying lateral corner detail values in the editor
        public enum CornerDetailEditMode { All, Individual }
        public CornerDetailEditMode cornerDetailMode = CornerDetailEditMode.All;

        public Vector3 boxSize = Vector3.one;//Box extents for the BoxCenter positioning mode
        public Vector3 boxOffset = Vector3.zero;//Box center for the BoxCenter positioning mode
        public Vector3 boxSidesPos = Vector3.one * 0.5f;//Three box side extents in positive cardinal directions for the BoxSides mode
        public Vector3 boxSidesNeg = Vector3.one * -0.5f;//Three box side extents in negative cardinal directions for the BoxSides mode

        //List of the eight corners of the collider; the size of the array should not be modified
        public ColliderCorner[] corners = new ColliderCorner[8]
        { new ColliderCorner(), new ColliderCorner(), new ColliderCorner(), new ColliderCorner(),
            new ColliderCorner(), new ColliderCorner(), new ColliderCorner(), new ColliderCorner() };

        public enum LateralCorner { FrontRight, FrontLeft, BackLeft, BackRight } //Identifies the four lateral corners on which detail applies
        public int[] cornerDetails = new int[4] { 2, 2, 2, 2 };//Detail amounts for each corner
        public int topSegments = 2;//Number of vertex strips adding detail on the top of the collider
        public int bottomSegments = 2;//Number of vertex strips adding detail on the bottom of the collider
        public int XYDetail = 1;//Number of detail edge loops in the X-Y plane
        public int YZDetail = 1;//Number of detail edge loops in the Y-Z plane
        public int XZDetail = 1;//Number of detail edge loops in the X-Z plane
        public float detailSmoothness = 0.0f;//Smoothness of intermittent detail vertices between corners

        //Adjusts the distribution of vertex strips for the top and bottom segments
        //Higher values move strips closer to the top while lower values move strips closer to the bottom
        public float stripDistribution1 = 1.9f;
        public float stripDistribution2 = 1.1f;

        public DeformHook[] hooks = new DeformHook[0];//Hooks for deforming the collider

        public bool bypassPolyTest = false;//Whether to skip polygon count testing
        public ColliderGenerator.PolygonTestMode polyTestMode = ColliderGenerator.PolygonTestMode.BestGuess;//Method of polygon testing

        //Modes for automatically reducing the detail of the collider
        //None disables automatic detail reduction
        //All reduces all detail values together
        //LargestFirst reduces the largest detail values first
        public enum DetailReductionMode { None, All, LargestFirst }
        public DetailReductionMode detailReduction = DetailReductionMode.LargestFirst;
        public int detailReductionAttempts = 10;//Maximum tries to reduce the detail before giving up

        //Determines if the corners are aligned in a simple box shape, otherwise positioned freely
        //This is used with polygon tests based on guessing
        public bool boxedCorners = false;

        //Creates a new instance of collider properties with defaults
        public GeneratorProps()
        {
            corners[0].localPos = new Vector3(0.5f, -0.5f, 0.5f);
            corners[1].localPos = new Vector3(-0.5f, -0.5f, 0.5f);
            corners[2].localPos = new Vector3(-0.5f, -0.5f, -0.5f);
            corners[3].localPos = new Vector3(0.5f, -0.5f, -0.5f);
            corners[4].localPos = new Vector3(0.5f, 0.5f, 0.5f);
            corners[5].localPos = new Vector3(-0.5f, 0.5f, 0.5f);
            corners[6].localPos = new Vector3(-0.5f, 0.5f, -0.5f);
            corners[7].localPos = new Vector3(0.5f, 0.5f, -0.5f);

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i].SetCornerLocationFromPosition();
                corners[i].axleRadii = Vector3.one * 0.1f;
            }
        }

        //Creates a new instance of collider properties in a box shape with the given size and radius for all corners
        public GeneratorProps(Vector3 size, float cornerRadius)
        {
            size *= 0.5f;
            corners[0].localPos = new Vector3(size.x, -size.y, size.z);
            corners[1].localPos = new Vector3(-size.x, -size.y, size.z);
            corners[2].localPos = new Vector3(-size.x, -size.y, -size.z);
            corners[3].localPos = new Vector3(size.x, -size.y, -size.z);
            corners[4].localPos = new Vector3(size.x, size.y, size.z);
            corners[5].localPos = new Vector3(-size.x, size.y, size.z);
            corners[6].localPos = new Vector3(-size.x, size.y, -size.z);
            corners[7].localPos = new Vector3(size.x, size.y, -size.z);

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i].SetCornerLocationFromPosition();
                corners[i].axleRadii = Vector3.one * cornerRadius;
            }
        }

        //Creates a new instance of collider properties by copying from an existing one
        public GeneratorProps(GeneratorProps gp)
        {
            CopyProperties(gp, true);
        }

        //Makes sure properties are set correctly with valid values before generation takes place
        public void VerifyProperties()
        {
            //Limiting corner radii to be at least zero
            for (int i = 0; i < corners.Length; i++)
            {
                corners[i].axleRadii = new Vector3(
                    Mathf.Max(0.0f, corners[i].axleRadii.x),
                    Mathf.Max(0.0f, corners[i].axleRadii.y),
                    Mathf.Max(0.0f, corners[i].axleRadii.z));
            }

            //Limiting details to be at least zero
            for (int i = 0; i < cornerDetails.Length; i++)
            {
                cornerDetails[i] = Mathf.Max(0, cornerDetails[i]);
            }

            topSegments = Mathf.Max(0, topSegments);
            bottomSegments = Mathf.Max(0, bottomSegments);
            XYDetail = Mathf.Max(0, XYDetail);
            YZDetail = Mathf.Max(0, YZDetail);
            XZDetail = Mathf.Max(0, XZDetail);
            detailSmoothness = Mathf.Clamp01(detailSmoothness);
            detailReductionAttempts = Mathf.Max(0, detailReductionAttempts);

            //Determining boxed state (indicates that the collider is going to have simpler geometry)
            boxedCorners = cornerPositionMode != CornerPositionEditMode.Individual
                && cornerRadiusMode == CornerRadiusEditMode.Uniform
                && cornerOffsetMode == CornerRadiusEditMode.Uniform && hooks.Length == 0;

            //Limiting hook properties
            for (int i = 0; i < hooks.Length; i++)
            {
                hooks[i].radius = Mathf.Max(0.0f, hooks[i].radius);
                hooks[i].falloff = Mathf.Max(0.01f, hooks[i].falloff);
            }
        }

        //Copies properties from another GeneratorProps instance
        //If copyPreset is true, it will also copy the linked preset reference from the other instance
        public void CopyProperties(GeneratorProps gp, bool copyPreset)
        {
            if (copyPreset)
            {
                linkedPreset = gp.linkedPreset;
            }

            meshAssetPath = gp.meshAssetPath;
            cornerPositionMode = gp.cornerPositionMode;
            cornerRadiusMode = gp.cornerRadiusMode;
            cornerOffsetMode = gp.cornerOffsetMode;
            cornerDetailMode = gp.cornerDetailMode;
            boxSize = gp.boxSize;
            boxOffset = gp.boxOffset;
            boxSidesPos = gp.boxSidesPos;
            boxSidesNeg = gp.boxSidesNeg;
            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = new ColliderCorner(gp.corners[i]);
            }

            for (int i = 0; i < cornerDetails.Length; i++)
            {
                cornerDetails[i] = gp.cornerDetails[i];
            }

            topSegments = gp.topSegments;
            bottomSegments = gp.bottomSegments;
            XYDetail = gp.XYDetail;
            YZDetail = gp.YZDetail;
            XZDetail = gp.XZDetail;
            detailSmoothness = gp.detailSmoothness;
            stripDistribution1 = gp.stripDistribution1;
            stripDistribution2 = gp.stripDistribution2;
            bypassPolyTest = gp.bypassPolyTest;
            polyTestMode = gp.polyTestMode;
            detailReduction = gp.detailReduction;
            detailReductionAttempts = gp.detailReductionAttempts;
            boxedCorners = gp.boxedCorners;
            hooks = new DeformHook[gp.hooks.Length];

            for (int i = 0; i < hooks.Length; i++)
            {
                hooks[i] = new DeformHook(gp.hooks[i]);
            }
        }

        //Saves properties to the linked preset and returns true if successful
        public bool SavePropertiesToPreset()
        {
            if (linkedPreset != null)
            {
                linkedPreset.props = new GeneratorProps(this);
#if UNITY_EDITOR
                if (Application.isEditor)
                {
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

        //Loads properties from the linked preset and returns true if successful
        public bool LoadPropertiesFromPreset()
        {
            if (linkedPreset != null)
            {
                if (linkedPreset.props != null)
                {
                    CopyProperties(linkedPreset.props, false);
                    return true;
                }
                else
                {
                    Debug.LogWarning("Linked preset is missing properties to load from.");
                    return false;
                }
            }
            else
            {
                Debug.LogWarning("No linked preset to load from.");
                return false;
            }
        }

        //Sets the collider to be a box shape with the given size
        public void SetBox(Vector3 size)
        {
            boxOffset = Vector3.zero;
            boxSize = size;
            boxSidesPos = size * 0.5f;
            boxSidesNeg = size * -0.5f;

            if (cornerPositionMode == CornerPositionEditMode.Individual)
            {
                cornerPositionMode = CornerPositionEditMode.BoxCenter;
            }

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i].localPos = Vector3.Scale(corners[i].normalizedCornerLocation, size) * 0.5f;
            }
        }

        //Sets the collider to be a box shape with the given size and offset in local space
        public void SetBox(Vector3 offset, Vector3 size)
        {
            boxOffset = offset;
            boxSize = size;
            boxSidesPos = offset + size * 0.5f;
            boxSidesNeg = offset - size * 0.5f;

            if (cornerPositionMode == CornerPositionEditMode.Individual)
            {
                cornerPositionMode = CornerPositionEditMode.BoxCenter;
            }

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i].localPos = Vector3.Scale(corners[i].normalizedCornerLocation, size) * 0.5f + offset;
            }
        }

        //Sets the position of a corner with the given location identifier in local space
        public void SetCornerPosition(ColliderCorner.CornerId corner, Vector3 pos)
        {
            GetCornerAtLocation(corner).localPos = pos;
            cornerPositionMode = CornerPositionEditMode.Individual;
        }

        //Sets the radius of a corner with the given location identifier
        public void SetCornerRadius(ColliderCorner.CornerId corner, float radius)
        {
            GetCornerAtLocation(corner).axleRadii = Vector3.one * radius;
            cornerRadiusMode = CornerRadiusEditMode.UniformIndividual;
        }

        //Sets the radii of all corners to the value
        public void SetCornerRadiiAll(float radius)
        {
            cornerRadiusMode = CornerRadiusEditMode.Uniform;
            for (int i = 0; i < corners.Length; i++)
            {
                corners[i].axleRadii = Vector3.one * radius;
            }
        }

        //Sets the components of the radius of a corner to a Vector3
        public void SetCornerRadius(ColliderCorner.CornerId corner, Vector3 radius)
        {
            GetCornerAtLocation(corner).axleRadii = radius;
            cornerRadiusMode = CornerRadiusEditMode.Advanced;
        }

        //Sets the components of the radii of all corners to a Vector3
        public void SetCornerRadiiAll(Vector3 radius)
        {
            cornerRadiusMode = CornerRadiusEditMode.Advanced;
            for (int i = 0; i < corners.Length; i++)
            {
                corners[i].axleRadii = radius;
            }
        }

        //Sets the radius offset of a corner to the value
        public void SetCornerRadiusOffset(ColliderCorner.CornerId corner, float offset)
        {
            cornerOffsetMode = CornerRadiusEditMode.UniformIndividual;
            GetCornerAtLocation(corner).radiusOffsets = Vector3.one * offset;
        }

        //Sets the radius offsets of all corners to the value
        public void SetCornerRadiusOffsetsAll(float offset)
        {
            cornerOffsetMode = CornerRadiusEditMode.Uniform;
            for (int i = 0; i < corners.Length; i++)
            {
                corners[i].radiusOffsets = Vector3.one * offset;
            }
        }

        //Sets the components of the radius offset of a corner to a Vector3
        public void SetCornerRadiusOffset(ColliderCorner.CornerId corner, Vector3 offset)
        {
            cornerOffsetMode = CornerRadiusEditMode.Advanced;
            GetCornerAtLocation(corner).radiusOffsets = offset;
        }

        //Sets the components of the radius offsets of all corners to a Vector3
        public void SetCornerRadiusOffsetsAll(Vector3 offset)
        {
            cornerOffsetMode = CornerRadiusEditMode.Advanced;
            for (int i = 0; i < corners.Length; i++)
            {
                corners[i].radiusOffsets = offset;
            }
        }

        //Sets all corner details to the value
        public void SetCornerDetail(int detail)
        {
            cornerDetails[0] = detail;
            cornerDetails[1] = detail;
            cornerDetails[2] = detail;
            cornerDetails[3] = detail;
        }

        //Sets the detail of a certain lateral corner identified by the given enum
        public void SetCornerDetail(LateralCorner corner, int detail)
        {
            cornerDetails[(int)corner] = detail;
        }

        //Sets the details of all corners to the respective values
        public void SetCornerDetailsAll(int detail0, int detail1, int detail2, int detail3)
        {
            cornerDetails[0] = detail0;
            cornerDetails[1] = detail1;
            cornerDetails[2] = detail2;
            cornerDetails[3] = detail3;
        }

        //Gets the average local point between all corners
        public Vector3 GetAverageCornerPos()
        {
            return (corners[0].localPos + corners[1].localPos
                + corners[2].localPos + corners[3].localPos
                + corners[4].localPos + corners[5].localPos
                + corners[6].localPos + corners[7].localPos) / 8.0f;
        }

        //Gets the corner with the given location identifier
        public ColliderCorner GetCornerAtLocation(ColliderCorner.CornerId cId)
        {
            for (int i = 0; i < corners.Length; i++)
            {
                if (corners[i].cornerLocation == cId)
                {
                    return corners[i];
                }
            }
            Debug.LogWarning("No corner exists with the corner ID.");
            return null;
        }

        //Adds a new hook to the collider
        public void AddHook(DeformHook dh)
        {
            List<DeformHook> tempHooks = hooks.ToList();
            tempHooks.Add(dh);
            hooks = tempHooks.ToArray();
        }

        //Adds a new hook with the given properties to the collider
        public DeformHook AddHook(DeformHook.HookType hType, Vector3 pos, Quaternion rot, float radius, float strength, float falloff)
        {
            List<DeformHook> tempHooks = hooks.ToList();
            DeformHook newHook = new DeformHook("Hook", hType, pos, rot, radius, strength, falloff);
            tempHooks.Add(newHook);
            hooks = tempHooks.ToArray();
            return newHook;
        }

        //Adds a new hook with the given properties to the collider
        public DeformHook AddHook(string name, DeformHook.HookType hType, Vector3 pos, Quaternion rot, float radius, float strength, float falloff)
        {
            List<DeformHook> tempHooks = hooks.ToList();
            DeformHook newHook = new DeformHook(name, hType, pos, rot, radius, strength, falloff);
            tempHooks.Add(newHook);
            hooks = tempHooks.ToArray();
            return newHook;
        }

        //Removes a hook from the collider if it exists
        public void RemoveHook(DeformHook hook)
        {
            List<DeformHook> tempHooks = hooks.ToList();
            if (tempHooks.Contains(hook))
            {
                tempHooks.Remove(hook);
                hooks = tempHooks.ToArray();
            }
            else
            {
                Debug.LogWarning("Specified hook does not exist in hook list.");
            }
        }

        //Removes a hook from the collider at the index in the hook list
        public void RemoveHook(int index)
        {
            if (hooks.Length <= index)
            {
                Debug.LogWarning("Index is larger than hook list size.");
                return;
            }
            List<DeformHook> tempHooks = hooks.ToList();
            tempHooks.RemoveAt(index);
            hooks = tempHooks.ToArray();
        }

        //Resets the corners to form a basic box shape
        public void ResetCorners()
        {
            boxSize = Vector3.one;
            boxOffset = Vector3.zero;
            boxSidesPos = Vector3.one * 0.5f;
            boxSidesNeg = Vector3.one * -0.5f;

            for (int i = 0; i < corners.Length; i++)
            {
                ColliderCorner curCorner = corners[i];
                curCorner.localPos = Vector3.Scale(curCorner.normalizedCornerLocation, boxSize) * 0.5f + boxOffset;
                curCorner.ResetRadii();
            }
        }

        //Resets all detail values to their defaults
        public void ResetDetail()
        {
            stripDistribution1 = 1.9f;
            stripDistribution2 = 1.1f;
            cornerDetails[0] = 2;
            cornerDetails[1] = 2;
            cornerDetails[2] = 2;
            cornerDetails[3] = 2;
            topSegments = 2;
            bottomSegments = 2;
            XYDetail = 1;
            YZDetail = 1;
            XZDetail = 1;
            detailSmoothness = 1.0f;
        }

        //Sets corner positions based on box properties, called by the editor window after editing box properties
        public void SetCornerPositionsFromBox()
        {
            //Corner positioning based on boxSize and boxCenter
            if (cornerPositionMode == CornerPositionEditMode.BoxCenter)
            {
                for (int i = 0; i < corners.Length; i++)
                {
                    corners[i].localPos = Vector3.Scale(corners[i].normalizedCornerLocation, boxSize) * 0.5f + boxOffset;
                }
            }
            //Corner positioning based on boxSidesPos and boxSidesNeg
            else if (cornerPositionMode == CornerPositionEditMode.BoxSides)
            {
                for (int i = 0; i < corners.Length; i++)
                {
                    ColliderCorner curCorner = corners[i];
                    switch (curCorner.cornerLocation)
                    {
                        case ColliderCorner.CornerId.FrontTopRight:
                            curCorner.localPos = boxSidesPos;
                            break;
                        case ColliderCorner.CornerId.FrontTopLeft:
                            curCorner.localPos = new Vector3(boxSidesNeg.x, boxSidesPos.y, boxSidesPos.z);
                            break;
                        case ColliderCorner.CornerId.FrontBottomRight:
                            curCorner.localPos = new Vector3(boxSidesPos.x, boxSidesNeg.y, boxSidesPos.z);
                            break;
                        case ColliderCorner.CornerId.FrontBottomLeft:
                            curCorner.localPos = new Vector3(boxSidesNeg.x, boxSidesNeg.y, boxSidesPos.z);
                            break;
                        case ColliderCorner.CornerId.BackTopRight:
                            curCorner.localPos = new Vector3(boxSidesPos.x, boxSidesPos.y, boxSidesNeg.z);
                            break;
                        case ColliderCorner.CornerId.BackTopLeft:
                            curCorner.localPos = new Vector3(boxSidesNeg.x, boxSidesPos.y, boxSidesNeg.z);
                            break;
                        case ColliderCorner.CornerId.BackBottomRight:
                            curCorner.localPos = new Vector3(boxSidesPos.x, boxSidesNeg.y, boxSidesNeg.z);
                            break;
                        case ColliderCorner.CornerId.BackBottomLeft:
                            curCorner.localPos = boxSidesNeg;
                            break;
                    }
                }
            }
        }

        //Mirrors a collider by copying the side of the collider indicated by the Vector3 to the opposite side
        //mirrorSide indicates the side of the collider that will be mirrored
        //Input vector must be such that only one component is either 1 or -1 and the rest are 0
        public void Mirror(Vector3 mirrorSide)
        {
            Mirror(mirrorSide, ref hooks);
        }

        //Alternative mirror function with a supplied hook list to mirror
        //mirrorSide indicates the side of the collider that will be mirrored
        //Input vector must be such that only one component is either 1 or -1 and the rest are 0
        public void Mirror(Vector3 mirrorSide, ref DeformHook[] mHooks)
        {
            if (Mathf.RoundToInt(mirrorSide.x) == 1)
            {
                //Positive X to negative X mirroring
                boxSize.Set(boxSize.x + boxOffset.x * 2.0f, boxSize.y, boxSize.z);
                boxOffset.Set(0.0f, boxOffset.y, boxOffset.z);
                boxSidesNeg.Set(-boxSidesPos.x, boxSidesNeg.y, boxSidesNeg.z);

                MirrorOperation(new ColliderCorner.CornerId[8] {
                    ColliderCorner.CornerId.FrontTopRight, ColliderCorner.CornerId.FrontTopLeft,
                    ColliderCorner.CornerId.FrontBottomRight, ColliderCorner.CornerId.FrontBottomLeft,
                    ColliderCorner.CornerId.BackTopRight, ColliderCorner.CornerId.BackTopLeft,
                    ColliderCorner.CornerId.BackBottomRight, ColliderCorner.CornerId.BackBottomLeft },
                    ref mHooks,
                    -1, 1, 1, true);

                cornerDetails[1] = cornerDetails[0];
                cornerDetails[2] = cornerDetails[3];
            }
            else if (Mathf.RoundToInt(mirrorSide.x) == -1)
            {
                //Negative X to positive X mirroring
                boxSize.Set(boxSize.x - boxOffset.x * 2.0f, boxSize.y, boxSize.z);
                boxOffset.Set(0.0f, boxOffset.y, boxOffset.z);
                boxSidesPos.Set(-boxSidesNeg.x, boxSidesPos.y, boxSidesPos.z);

                MirrorOperation(new ColliderCorner.CornerId[8] {
                    ColliderCorner.CornerId.FrontTopLeft, ColliderCorner.CornerId.FrontTopRight,
                    ColliderCorner.CornerId.FrontBottomLeft, ColliderCorner.CornerId.FrontBottomRight,
                    ColliderCorner.CornerId.BackTopLeft, ColliderCorner.CornerId.BackTopRight,
                    ColliderCorner.CornerId.BackBottomLeft, ColliderCorner.CornerId.BackBottomRight },
                    ref mHooks,
                    -1, 1, 1, false);

                cornerDetails[0] = cornerDetails[1];
                cornerDetails[3] = cornerDetails[2];
            }
            else if (Mathf.RoundToInt(mirrorSide.y) == 1)
            {
                //Positive Y to negative Y mirroring
                boxSize.Set(boxSize.x, boxSize.y + boxOffset.y * 2.0f, boxSize.z);
                boxOffset.Set(boxOffset.x, 0.0f, boxOffset.z);
                boxSidesNeg.Set(boxSidesNeg.x, -boxSidesPos.y, boxSidesNeg.z);

                MirrorOperation(new ColliderCorner.CornerId[8] {
                    ColliderCorner.CornerId.FrontTopRight, ColliderCorner.CornerId.FrontBottomRight,
                    ColliderCorner.CornerId.FrontTopLeft, ColliderCorner.CornerId.FrontBottomLeft,
                    ColliderCorner.CornerId.BackTopRight, ColliderCorner.CornerId.BackBottomRight,
                    ColliderCorner.CornerId.BackTopLeft, ColliderCorner.CornerId.BackBottomLeft },
                    ref mHooks,
                    1, -1, 1, true);

                bottomSegments = topSegments;
            }
            else if (Mathf.RoundToInt(mirrorSide.y) == -1)
            {
                //Negative Y to positive Y mirroring
                boxSize.Set(boxSize.x, boxSize.y - boxOffset.y * 2.0f, boxSize.z);
                boxOffset.Set(boxOffset.x, 0.0f, boxOffset.z);
                boxSidesPos.Set(boxSidesPos.x, -boxSidesNeg.y, boxSidesPos.z);

                MirrorOperation(new ColliderCorner.CornerId[8] {
                    ColliderCorner.CornerId.FrontBottomRight, ColliderCorner.CornerId.FrontTopRight,
                    ColliderCorner.CornerId.FrontBottomLeft, ColliderCorner.CornerId.FrontTopLeft,
                    ColliderCorner.CornerId.BackBottomRight, ColliderCorner.CornerId.BackTopRight,
                    ColliderCorner.CornerId.BackBottomLeft, ColliderCorner.CornerId.BackTopLeft },
                    ref mHooks,
                    1, -1, 1, false);

                topSegments = bottomSegments;
            }
            else if (Mathf.RoundToInt(mirrorSide.z) == 1)
            {
                //Positive Z to negative Z mirroring
                boxSize.Set(boxSize.x, boxSize.y, boxSize.z + boxOffset.z * 2.0f);
                boxOffset.Set(boxOffset.x, boxOffset.y, 0.0f);
                boxSidesNeg.Set(boxSidesNeg.x, boxSidesNeg.y, -boxSidesPos.z);

                MirrorOperation(new ColliderCorner.CornerId[8] {
                    ColliderCorner.CornerId.FrontTopRight, ColliderCorner.CornerId.BackTopRight,
                    ColliderCorner.CornerId.FrontTopLeft, ColliderCorner.CornerId.BackTopLeft,
                    ColliderCorner.CornerId.FrontBottomRight, ColliderCorner.CornerId.BackBottomRight,
                    ColliderCorner.CornerId.FrontBottomLeft, ColliderCorner.CornerId.BackBottomLeft },
                    ref mHooks,
                    1, 1, -1, true);

                cornerDetails[3] = cornerDetails[0];
                cornerDetails[2] = cornerDetails[1];
            }
            else if (Mathf.RoundToInt(mirrorSide.z) == -1)
            {
                //Negative Z to positive Z mirroring
                boxSize.Set(boxSize.x, boxSize.y, boxSize.z - boxOffset.z * 2.0f);
                boxOffset.Set(boxOffset.x, boxOffset.y, 0.0f);
                boxSidesPos.Set(boxSidesPos.x, boxSidesPos.y, -boxSidesNeg.z);

                MirrorOperation(new ColliderCorner.CornerId[8] {
                    ColliderCorner.CornerId.BackTopRight, ColliderCorner.CornerId.FrontTopRight,
                    ColliderCorner.CornerId.BackTopLeft, ColliderCorner.CornerId.FrontTopLeft,
                    ColliderCorner.CornerId.BackBottomRight, ColliderCorner.CornerId.FrontBottomRight,
                    ColliderCorner.CornerId.BackBottomLeft, ColliderCorner.CornerId.FrontBottomLeft },
                    ref mHooks,
                    1, 1, -1, false);

                cornerDetails[0] = cornerDetails[3];
                cornerDetails[1] = cornerDetails[2];
            }
            else
            {
                Debug.LogWarning("Could not mirror collider, verify that only one component of mirroring side is either 1 or -1.");
            }
        }

        //Function for mirroring corners
        //cids must be a list of corner ID pairs where each pair represents a getter and setter corner, where one corner's properties are being mirrored and copied to the other one
        void MirrorOperation(ColliderCorner.CornerId[] cids, ref DeformHook[] mHooks, int xFactor, int yFactor, int zFactor, bool isPositiveSide)
        {
            if (cids.Length != 8)
            {
                Debug.LogError("Mirror operation failed, corner input array must contain 8 corners (4 pairs of corners being mirrored).");
                return;
            }

            //Mirroring corners
            MirrorCorner(cids[0], cids[1], xFactor, yFactor, zFactor);
            MirrorCorner(cids[2], cids[3], xFactor, yFactor, zFactor);
            MirrorCorner(cids[4], cids[5], xFactor, yFactor, zFactor);
            MirrorCorner(cids[6], cids[7], xFactor, yFactor, zFactor);

            //Tests to make sure not to mirror hooks at the center of the flipping axis and remove hooks opposite of the mirroring side
            List<DeformHook> tempHooks = mHooks.ToList();
            for (int i = 0; i < tempHooks.Count; i++)
            {
                if (isPositiveSide && ((xFactor < 0 && tempHooks[i].localPos.x < -0.001f) || (yFactor < 0 && tempHooks[i].localPos.y < -0.001f) || (zFactor < 0 && tempHooks[i].localPos.z < -0.001f))
                    || !isPositiveSide && ((xFactor < 0 && tempHooks[i].localPos.x > 0.001f) || (yFactor < 0 && tempHooks[i].localPos.y > 0.001f) || (zFactor < 0 && tempHooks[i].localPos.z > 0.001f)))
                {
                    tempHooks[i] = null;
                }
            }
            tempHooks.RemoveAll(hook => hook == null);

            //Mirroring hooks
            List<DeformHook> mirroredHooks = new List<DeformHook>();
            for (int i = 0; i < tempHooks.Count; i++)
            {
                if ((xFactor < 0 && Mathf.Abs(tempHooks[i].localPos.x) > 0.001f) || (yFactor < 0 && Mathf.Abs(tempHooks[i].localPos.y) > 0.001f) || (zFactor < 0 && Mathf.Abs(tempHooks[i].localPos.z) > 0.001f))
                {
                    DeformHook newHook = new DeformHook(tempHooks[i]);
                    if (!newHook.name.EndsWith("Mirrored"))
                    {
                        newHook.name += " Mirrored";
                    }
                    newHook.localPos.Set(xFactor * newHook.localPos.x, yFactor * newHook.localPos.y, zFactor * newHook.localPos.z);

                    Vector3 forwardDir = newHook.localRot * Vector3.forward;
                    Vector3 upDir = newHook.localRot * Vector3.up;
                    if (newHook.hookType != DeformHook.HookType.Twist)
                    {
                        //Mirroring rules for pull and expand hook types
                        Vector3 flippedForwardDir = new Vector3(xFactor * forwardDir.x, yFactor * forwardDir.y, zFactor * forwardDir.z);
                        Vector3 flippedUpDir = new Vector3(xFactor * upDir.x, yFactor * upDir.y, zFactor * upDir.z);
                        newHook.localRot = Quaternion.LookRotation(flippedForwardDir, flippedUpDir);
                    }
                    else
                    {
                        //Special rules for mirroring twist hook types
                        if (xFactor == -1)
                        {
                            Vector3 flippedForwardDir = new Vector3(xFactor * forwardDir.x, yFactor * forwardDir.y, zFactor * forwardDir.z);
                            Vector3 flippedUpDir = new Vector3(xFactor * upDir.x, yFactor * upDir.y, zFactor * upDir.z);
                            newHook.localRot = Quaternion.LookRotation(flippedForwardDir, flippedUpDir);
                        }
                        else if (yFactor == -1)
                        {
                            Vector3 flippedForwardDir = new Vector3(xFactor * forwardDir.x, yFactor * forwardDir.y, zFactor * forwardDir.z);
                            Vector3 flippedUpDir = new Vector3(yFactor * upDir.x, upDir.y, yFactor * upDir.z);
                            newHook.localRot = Quaternion.LookRotation(flippedForwardDir, flippedUpDir);
                        }
                        else if (zFactor == -1)
                        {
                            Vector3 flippedForwardDir = new Vector3(zFactor * forwardDir.x, zFactor * forwardDir.y, forwardDir.z);
                            Vector3 flippedUpDir = new Vector3(zFactor * upDir.x, yFactor * upDir.y, zFactor * upDir.z);
                            newHook.localRot = Quaternion.LookRotation(flippedForwardDir, flippedUpDir);
                        }
                    }
                    newHook.localEulerRot = newHook.localRot.eulerAngles;
                    mirroredHooks.Add(newHook);
                }
            }
            tempHooks.AddRange(mirroredHooks);
            mHooks = tempHooks.ToArray();
        }

        //Flips a collider in the specified direction
        //This does not copy properties from one side to the other, but swaps them instead
        //Input vector must be such that only one component is either 1 or -1 and the rest are 0
        public void Flip(Vector3 flipDir)
        {
            Flip(flipDir, ref hooks);
        }

        //Alternative flip function with a supplied hook list to flip
        //This does not copy properties from one side to the other, but swaps them instead
        //Input vector must be such that only one component is either 1 or -1 and the rest are 0
        public void Flip(Vector3 flipDir, ref DeformHook[] mHooks)
        {
            if (Mathf.RoundToInt(flipDir.x) == 1)
            {
                //X-axis flipping
                boxOffset.Set(-boxOffset.x, boxOffset.y, boxOffset.z);
                float tempX = boxSidesNeg.x;
                boxSidesNeg.Set(-boxSidesPos.x, boxSidesNeg.y, boxSidesNeg.z);
                boxSidesPos.Set(-tempX, boxSidesPos.y, boxSidesPos.z);

                FlipOperation(new ColliderCorner.CornerId[8] {
                    ColliderCorner.CornerId.FrontTopRight, ColliderCorner.CornerId.FrontTopLeft,
                    ColliderCorner.CornerId.FrontBottomRight, ColliderCorner.CornerId.FrontBottomLeft,
                    ColliderCorner.CornerId.BackTopRight, ColliderCorner.CornerId.BackTopLeft,
                    ColliderCorner.CornerId.BackBottomRight, ColliderCorner.CornerId.BackBottomLeft },
                    ref mHooks,
                    -1, 1, 1);

                int tempDetail0 = cornerDetails[0];
                int tempDetail1 = cornerDetails[2];
                cornerDetails[0] = cornerDetails[1];
                cornerDetails[1] = tempDetail0;
                cornerDetails[2] = cornerDetails[3];
                cornerDetails[3] = tempDetail1;
            }
            else if (Mathf.RoundToInt(flipDir.y) == 1)
            {
                //Y-axis flipping
                boxOffset.Set(boxOffset.x, -boxOffset.y, boxOffset.z);
                float tempY = boxSidesNeg.y;
                boxSidesNeg.Set(boxSidesNeg.x, -boxSidesPos.y, boxSidesNeg.z);
                boxSidesPos.Set(boxSidesPos.x, -tempY, boxSidesPos.z);

                FlipOperation(new ColliderCorner.CornerId[8] {
                    ColliderCorner.CornerId.FrontTopRight, ColliderCorner.CornerId.FrontBottomRight,
                    ColliderCorner.CornerId.FrontTopLeft, ColliderCorner.CornerId.FrontBottomLeft,
                    ColliderCorner.CornerId.BackTopRight, ColliderCorner.CornerId.BackBottomRight,
                    ColliderCorner.CornerId.BackTopLeft, ColliderCorner.CornerId.BackBottomLeft },
                    ref mHooks,
                    1, -1, 1);

                int tempSegments = bottomSegments;
                bottomSegments = topSegments;
                topSegments = tempSegments;
            }
            else if (Mathf.RoundToInt(flipDir.z) == 1)
            {
                //Z-axis flipping
                boxOffset.Set(boxOffset.x, boxOffset.y, -boxOffset.z);
                float tempZ = boxSidesNeg.z;
                boxSidesNeg.Set(boxSidesNeg.x, boxSidesNeg.y, -boxSidesPos.z);
                boxSidesPos.Set(boxSidesPos.x, boxSidesPos.y, -tempZ);

                FlipOperation(new ColliderCorner.CornerId[8] {
                    ColliderCorner.CornerId.FrontTopRight, ColliderCorner.CornerId.BackTopRight,
                    ColliderCorner.CornerId.FrontTopLeft, ColliderCorner.CornerId.BackTopLeft,
                    ColliderCorner.CornerId.FrontBottomRight, ColliderCorner.CornerId.BackBottomRight,
                    ColliderCorner.CornerId.FrontBottomLeft, ColliderCorner.CornerId.BackBottomLeft },
                    ref mHooks,
                    1, 1, -1);

                int tempDetail0 = cornerDetails[1];
                int tempDetail1 = cornerDetails[0];
                cornerDetails[0] = cornerDetails[3];
                cornerDetails[1] = cornerDetails[2];
                cornerDetails[2] = tempDetail0;
                cornerDetails[3] = tempDetail1;
            }
            else
            {
                Debug.LogWarning("Could not flip collider, verify that only one component of flipping direction is 1.");
            }
        }

        //Function for flipping corners
        //cids must be a list of corner ID pairs where each pair represents corners being swapped
        void FlipOperation(ColliderCorner.CornerId[] cids, ref DeformHook[] mHooks, int xFactor, int yFactor, int zFactor)
        {
            if (cids.Length != 8)
            {
                Debug.LogError("Mirror operation failed, corner input array must contain 8 corners (4 pairs of corners being mirrored).");
                return;
            }

            //Temporary corners for swapping
            ColliderCorner[] tempCorners = new ColliderCorner[4]
            { new ColliderCorner(GetCornerAtLocation(cids[1])), new ColliderCorner(GetCornerAtLocation(cids[3])),
                new ColliderCorner(GetCornerAtLocation(cids[5])), new ColliderCorner(GetCornerAtLocation(cids[7])) };

            //First part of flipping by mirroring
            MirrorCorner(cids[0], cids[1], xFactor, yFactor, zFactor);
            MirrorCorner(cids[2], cids[3], xFactor, yFactor, zFactor);
            MirrorCorner(cids[4], cids[5], xFactor, yFactor, zFactor);
            MirrorCorner(cids[6], cids[7], xFactor, yFactor, zFactor);

            //Second part of flipping by mirroring temporary corners
            MirrorCorner(tempCorners[0], cids[0], xFactor, yFactor, zFactor);
            MirrorCorner(tempCorners[1], cids[2], xFactor, yFactor, zFactor);
            MirrorCorner(tempCorners[2], cids[4], xFactor, yFactor, zFactor);
            MirrorCorner(tempCorners[3], cids[6], xFactor, yFactor, zFactor);

            //Flipping hooks
            for (int i = 0; i < mHooks.Length; i++)
            {
                DeformHook curHook = mHooks[i];
                curHook.localPos.Set(xFactor * curHook.localPos.x, yFactor * curHook.localPos.y, zFactor * curHook.localPos.z);

                Vector3 forwardDir = curHook.localRot * Vector3.forward;
                Vector3 upDir = curHook.localRot * Vector3.up;
                if (curHook.hookType != DeformHook.HookType.Twist)
                {
                    //Flipping rules for pull and expand hook types
                    Vector3 flippedForwardDir = new Vector3(xFactor * forwardDir.x, yFactor * forwardDir.y, zFactor * forwardDir.z);
                    Vector3 flippedUpDir = new Vector3(xFactor * upDir.x, yFactor * upDir.y, zFactor * upDir.z);
                    curHook.localRot = Quaternion.LookRotation(flippedForwardDir, flippedUpDir);
                }
                else
                {
                    //Special rules for flipping twist hook types
                    if (xFactor == -1)
                    {
                        Vector3 flippedForwardDir = new Vector3(xFactor * forwardDir.x, yFactor * forwardDir.y, zFactor * forwardDir.z);
                        Vector3 flippedUpDir = new Vector3(xFactor * upDir.x, yFactor * upDir.y, zFactor * upDir.z);
                        curHook.localRot = Quaternion.LookRotation(flippedForwardDir, flippedUpDir);
                    }
                    else if (yFactor == -1)
                    {
                        Vector3 flippedForwardDir = new Vector3(xFactor * forwardDir.x, yFactor * forwardDir.y, zFactor * forwardDir.z);
                        Vector3 flippedUpDir = new Vector3(yFactor * upDir.x, upDir.y, yFactor * upDir.z);
                        curHook.localRot = Quaternion.LookRotation(flippedForwardDir, flippedUpDir);
                    }
                    else if (zFactor == -1)
                    {
                        Vector3 flippedForwardDir = new Vector3(zFactor * forwardDir.x, zFactor * forwardDir.y, forwardDir.z);
                        Vector3 flippedUpDir = new Vector3(zFactor * upDir.x, yFactor * upDir.y, zFactor * upDir.z);
                        curHook.localRot = Quaternion.LookRotation(flippedForwardDir, flippedUpDir);
                    }
                }
                curHook.localEulerRot = curHook.localRot.eulerAngles;
            }
        }

        //Mirrors a corner by copying properties from the getter corner to the setter corner
        //"setter" indicates being set by the "getter"
        void MirrorCorner(ColliderCorner.CornerId getter, ColliderCorner.CornerId setter, int xFactor, int yFactor, int zFactor)
        {
            ColliderCorner getCorner = GetCornerAtLocation(getter);
            ColliderCorner setCorner = GetCornerAtLocation(setter);
            setCorner.localPos.Set(xFactor * getCorner.localPos.x, yFactor * getCorner.localPos.y, zFactor * getCorner.localPos.z);
            setCorner.axleRadii = getCorner.axleRadii;
            setCorner.radiusOffsets = getCorner.radiusOffsets;
        }

        //Alternate mirroring function where the getter is a reference to an actual corner instead of a corner location identifier
        void MirrorCorner(ColliderCorner getter, ColliderCorner.CornerId setter, int xFactor, int yFactor, int zFactor)
        {
            ColliderCorner setCorner = GetCornerAtLocation(setter);
            setCorner.localPos.Set(xFactor * getter.localPos.x, yFactor * getter.localPos.y, zFactor * getter.localPos.z);
            setCorner.axleRadii = getter.axleRadii;
            setCorner.radiusOffsets = getter.radiusOffsets;
        }
    }
}