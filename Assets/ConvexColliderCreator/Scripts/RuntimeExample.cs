// Copyright (c) 2018 Justin Couch / JustInvoke
using UnityEngine;
using UnityEngine.UI;

namespace ConvexColliderCreator
{
    //Example class for the runtime collider generation demo
    public class RuntimeExample : MonoBehaviour
    {
        public GameObject target;//Target object holding the collider
        Vector3 initialPos = Vector3.zero;//Resetting position
        ColliderGroup colGroup;//Collider group on the target object
        GeneratorProps colProps;//Collider properties on the target object
        public Text actionButtonText;//UI text on the drop/reset button

        private void Awake()
        {
            //Set up collider group and starting position
            if (target != null)
            {
                initialPos = target.transform.position;
                colGroup = target.AddComponent<ColliderGroup>();
            }
        }

        private void Update()
        {
            if (target == null || colGroup == null)
            {
                return;
            }

            if (colGroup.colliders.Count == 0)
            {
                //Add a collider to the group if it doesn't exist and add a deformation hook to it
                colProps = colGroup.AddCollider().props;
                colProps.AddHook(DeformHook.HookType.Pull, Vector3.zero, Quaternion.LookRotation(Vector3.up, Vector3.forward), 1.0f, 0.0f, 1.0f);
                GenerateCollider();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Application.Quit();
            }
        }

        //Generates the collider and visualization
        void GenerateCollider()
        {
            if (target != null && colProps != null)
            {
                colGroup.GenerateAllColliders();
                //Use generated collision mesh for rendering
                target.GetComponent<MeshFilter>().sharedMesh = colGroup.colliders[0].colMesh;
                target.GetComponent<MeshFilter>().sharedMesh.RecalculateNormals();
            }
        }

        //Sets the z-axis size of the collider
        public void SetColliderLength(float length)
        {
            if (colProps != null)
            {
                colProps.SetBox(new Vector3(colProps.boxSize.x, colProps.boxSize.y, length));
                GenerateCollider();
            }
        }

        //Sets the x-axis size of the collider
        public void SetColliderWidth(float width)
        {
            if (colProps != null)
            {
                colProps.SetBox(new Vector3(width, colProps.boxSize.y, colProps.boxSize.z));
                GenerateCollider();
            }
        }

        //Sets the y-axis size of the collider
        public void SetColliderHeight(float height)
        {
            if (colProps != null)
            {
                colProps.SetBox(new Vector3(colProps.boxSize.x, height, colProps.boxSize.z));
                GenerateCollider();
            }
        }

        //Sets the radius of all corners on the collider
        public void SetColliderCornerRadii(float radius)
        {
            if (colProps != null)
            {
                colProps.SetCornerRadiiAll(radius);
                GenerateCollider();
            }
        }

        //Sets the detail properties of the collider
        public void SetColliderDetail(float detail)
        {
            if (colProps != null)
            {
                int roundedDetail = Mathf.FloorToInt(detail);
                colProps.SetCornerDetail(roundedDetail);
                colProps.topSegments = roundedDetail;
                colProps.bottomSegments = roundedDetail;
                colProps.XYDetail = Mathf.Min(roundedDetail, 2);
                colProps.YZDetail = Mathf.Min(roundedDetail, 2);
                colProps.XZDetail = Mathf.Min(roundedDetail, 2);
                GenerateCollider();
            }
        }

        //Sets the x-position of the deformation hook
        public void SetHookXPosition(float xPos)
        {
            if (colProps != null)
            {
                if (colProps.hooks.Length > 0)
                {
                    colProps.hooks[0].localPos = new Vector3(xPos, colProps.hooks[0].localPos.y, colProps.hooks[0].localPos.z);
                    GenerateCollider();
                }
            }
        }

        //Sets the y-position of the deformation hook
        public void SetHookYPosition(float yPos)
        {
            if (colProps != null)
            {
                if (colProps.hooks.Length > 0)
                {
                    colProps.hooks[0].localPos = new Vector3(colProps.hooks[0].localPos.x, yPos, colProps.hooks[0].localPos.z);
                    GenerateCollider();
                }
            }
        }

        //Sets the z-position of the deformation hook
        public void SetHookZPosition(float zPos)
        {
            if (colProps != null)
            {
                if (colProps.hooks.Length > 0)
                {
                    colProps.hooks[0].localPos = new Vector3(colProps.hooks[0].localPos.x, colProps.hooks[0].localPos.y, zPos);
                    GenerateCollider();
                }
            }
        }

        //Sets the radius of the deformation hook
        public void SetHookRadius(float radius)
        {
            if (colProps != null)
            {
                if (colProps.hooks.Length > 0)
                {
                    colProps.hooks[0].radius = radius;
                    GenerateCollider();
                }
            }
        }

        //Sets the strength of the deformation hook
        public void SetHookStrength(float strength)
        {
            if (colProps != null)
            {
                if (colProps.hooks.Length > 0)
                {
                    colProps.hooks[0].strength = strength;
                    GenerateCollider();
                }
            }
        }

        //Randomizes the positions and radii of the corners
        public void RandomizeCollider()
        {
            if (colProps != null)
            {
                for (int i = 0; i < colProps.corners.Length; i++)
                {
                    colProps.corners[i].localPos = Vector3.Scale(colProps.corners[i].normalizedCornerLocation,
                        new Vector3(Random.Range(0.1f, 1.5f), Random.Range(0.1f, 1.5f), Random.Range(0.1f, 1.5f)));
                    colProps.corners[i].axleRadii = new Vector3(Random.Range(0.0f, 0.5f), Random.Range(0.0f, 0.5f), Random.Range(0.0f, 0.5f));
                }
                GenerateCollider();
            }
        }

        //Either drops or resets the target depending on its state
        public void DoTargetAction()
        {
            if (target != null)
            {
                if (target.GetComponent<Rigidbody>().isKinematic)
                {
                    DropTarget();
                }
                else
                {
                    ResetTarget();
                }
            }
        }

        //Drops the target
        void DropTarget()
        {
            if (target != null)
            {
                target.GetComponent<Rigidbody>().isKinematic = false;
                target.GetComponent<Rigidbody>().AddForce(Vector3.up * 2.0f, ForceMode.VelocityChange);
                target.GetComponent<Rigidbody>().AddTorque(Random.rotationUniform * Vector3.forward * Random.Range(0.5f, 5.0f), ForceMode.VelocityChange);

                if (actionButtonText != null)
                {
                    actionButtonText.text = "Reset";
                }
            }
        }

        //Resets the target to its initial position
        void ResetTarget()
        {
            if (target != null)
            {
                target.GetComponent<Rigidbody>().isKinematic = true;
                target.GetComponent<Rigidbody>().velocity = Vector3.zero;
                target.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
                target.transform.position = initialPos;
                target.transform.rotation = Quaternion.identity;

                if (actionButtonText != null)
                {
                    actionButtonText.text = "Drop";
                }
            }
        }
    }
}
