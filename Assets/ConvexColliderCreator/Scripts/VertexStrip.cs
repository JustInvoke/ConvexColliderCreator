// Copyright (c) 2018 Justin Couch / JustInvoke
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ConvexColliderCreator
{
    //Class for strip of vertices around a mesh, basically a lateral cross section
    //This class is only intended for internal use with collider generation and it's not recommended that you create instances of it
    public class VertexStrip
    {
        VertexStripProps props;//Properties of the strip
        public List<Vector3> verts = new List<Vector3>();//Vertices in the strip

        //The vertices in each of the four "sides" of the strip around the mesh
        public List<Vector3> side0 = new List<Vector3>();
        public List<Vector3> side1 = new List<Vector3>();
        public List<Vector3> side2 = new List<Vector3>();
        public List<Vector3> side3 = new List<Vector3>();

        //Vertices used to assist in the generation of the top and bottom caps of the collision mesh
        public List<Vector3> capVerts = new List<Vector3>();

        public VertexStrip() { } //Empty constructor

        //Constructor with properties to use
        public VertexStrip(VertexStripProps setProps)
        {
            props = setProps;
            Vector3 center = Vector3.zero;//Center of each corner
            Vector3 nextCenter = Vector3.zero;//Center of subsequent corner
            Vector3 newVertex = Vector3.zero;//Current vertex being added
            Vector3 nextVertex = Vector3.zero;//Next vertex along strip
            float increment = 0.0f;//Angle between vertices in each corner
            float angle = 0.0f;//Current angle of the vertex in a corner
            int iteration = 0;//Current being generated
            float radiusScale = 1.0f;//Scale of each corner
            float sqrtTwo = Mathf.Sqrt(2.0f);//Constant used to avoid repeat calculations

            //Looping through each corner
            while (iteration < 4)
            {
                //Vertex angle is incremented based on corner detail
                increment = props.cornerDetails[iteration] > 0 ?
                    (Mathf.PI * 0.5f) / props.cornerDetails[iteration] :
                    Mathf.PI * 0.25f;

                angle = Mathf.PI * 0.5f * iteration;//Starting angle in a corner

                int curCornerIndex = (props.isTop ? iteration + 4 : iteration);
                ColliderCorner curCorner = props.corners[curCornerIndex];
                ColliderCorner nextCorner = props.corners[(iteration == 3 ? (props.isTop ? 4 : 0) : curCornerIndex + 1)];

                center = curCorner.localPos + curCorner.axleRadii.y * (props.isTop ? Vector3.up : Vector3.down) * (props.flat ? 1.0f : props.cornerProgress);
                nextCenter = nextCorner.localPos + nextCorner.axleRadii.y * (props.isTop ? Vector3.up : Vector3.down) * (props.flat ? 1.0f : props.cornerProgress);
                radiusScale = Mathf.Sqrt(1.0f - Mathf.Pow(Mathf.Clamp01(props.cornerProgress), 2.0f));//Implicit formula of unit circle solved for y above x-axis, x^2 + y^2 = 1 => y = sqrt(1 - x^2)

                Vector3 curRadiusOffset = curCorner.GetOffset();
                Vector3 nextRadiusOffset = nextCorner.GetOffset();

                if (props.cornerDetails[iteration] == 0)
                {
                    //If corners are right angles
                    angle += increment;
                    radiusScale *= sqrtTwo;//This is to keep the collider the same size when the corner detail is zero. Pythagorean Theorem: 1 + 1 = c^2 => sqrt(2) = c
                    newVertex = center - curRadiusOffset + new Vector3(curCorner.axleRadii.x * radiusScale * Mathf.Cos(angle), 0.0f, curCorner.axleRadii.z * radiusScale * Mathf.Sin(angle));
                    verts.Add(newVertex);
                    nextVertex = nextCenter - nextRadiusOffset + new Vector3(nextCorner.axleRadii.x * radiusScale * Mathf.Cos(angle + increment * 2.0f), 0.0f, nextCorner.axleRadii.z * radiusScale * Mathf.Sin(angle + increment * 2.0f));
                }
                else
                {
                    //If corners are round
                    for (int i = 0; i < props.cornerDetails[iteration] + 1; i++)
                    {
                        newVertex = center - curRadiusOffset + new Vector3(curCorner.axleRadii.x * radiusScale * Mathf.Cos(angle), 0.0f, curCorner.axleRadii.z * radiusScale * Mathf.Sin(angle));
                        verts.Add(newVertex);
                        angle += increment;
                    }
                    nextVertex = nextCenter - nextRadiusOffset + new Vector3(nextCorner.axleRadii.x * radiusScale * Mathf.Cos(Mathf.PI * (iteration + 1) * 0.5f), 0.0f, nextCorner.axleRadii.z * radiusScale * Mathf.Sin(Mathf.PI * (iteration + 1) * 0.5f));
                }

                if (props.XYDetail > 0 && (iteration == 1 || iteration == 3))
                {
                    //Adding detail along sides of strip orthogonal to the X-Y plane
                    int curDetail = 1;
                    while (curDetail < props.XYDetail + 1)
                    {
                        float progress = ((curDetail * 1.0f) / ((props.XYDetail + 1) * 1.0f));
                        float smoothPower = Mathf.Pow(progress, 2.0f - progress * 2.0f);
                        verts.Add(Vector3.Lerp(Vector3.Lerp(newVertex, nextVertex, progress),
                            new Vector3(Mathf.Lerp(newVertex.x, nextVertex.x, smoothPower),
                            Mathf.Lerp(newVertex.y, nextVertex.y, smoothPower),
                            Mathf.Lerp(newVertex.z, nextVertex.z, progress)),
                            props.detailSmoothness));
                        curDetail++;
                    }
                }

                if (props.YZDetail > 0 && (iteration == 0 || iteration == 2))
                {
                    //Adding detail along sides of strip orthogonal to the Y-Z plane
                    int curDetail = 1;
                    while (curDetail < props.YZDetail + 1)
                    {
                        float progress = ((curDetail * 1.0f) / ((props.YZDetail + 1) * 1.0f));
                        float smoothPower = Mathf.Pow(progress, 2.0f - progress * 2.0f);
                        verts.Add(Vector3.Lerp(Vector3.Lerp(newVertex, nextVertex, progress),
                            new Vector3(Mathf.Lerp(newVertex.x, nextVertex.x, progress),
                            Mathf.Lerp(newVertex.y, nextVertex.y, smoothPower),
                            Mathf.Lerp(newVertex.z, nextVertex.z, smoothPower)),
                            props.detailSmoothness));
                        curDetail++;
                    }
                }

                iteration++;
            }
            verts.Add(verts[0]);//Extra copy of first vertex to close the strip
        }

        //Creates a vertex strip bridging two other strips, interpolated between by a certain amount based on progress
        public VertexStrip(VertexStrip bottomStrip, VertexStrip topStrip, float progress, float smoothness)
        {
            if (bottomStrip.verts.Count == topStrip.verts.Count)
            {
                for (int i = 0; i < bottomStrip.verts.Count; i++)
                {
                    float smoothPower = Mathf.Pow(progress, 2.0f - progress * 2.0f);
                    verts.Add(
                        Vector3.Lerp(Vector3.Lerp(bottomStrip.verts[i], topStrip.verts[i], progress),
                            new Vector3(Mathf.Lerp(bottomStrip.verts[i].x, topStrip.verts[i].x, smoothPower),
                        Mathf.Lerp(bottomStrip.verts[i].y, topStrip.verts[i].y, progress),
                        Mathf.Lerp(bottomStrip.verts[i].z, topStrip.verts[i].z, smoothPower)),
                            smoothness));
                }
            }
            else
            {
                Debug.LogWarning("Cannot create average vertex strip because reference strips have different vertex counts.");
            }
        }

        //Removes corner vertices from strip, used for generating the caps because they only need the side vertices
        //Vertices contained in the sides of the strip are excluded
        void RemoveCornerVerts(params List<int>[] excludedVerts)
        {
            if (verts.Count == 0)
            {
                return;
            }

            List<NullableVector3> newVerts = new List<NullableVector3>();
            for (int i = 0; i < verts.Count; i++)
            {
                newVerts.Add(new NullableVector3(verts[i]));
            }

            //Nullify all vertices that are not contained in one of the sides
            for (int i = 0; i < verts.Count; i++)
            {
                if (!ListsContainVert(i, excludedVerts))
                {
                    newVerts[i].Nullify();
                }
            }

            //Clear all nullified verts from strip
            verts.Clear();
            for (int i = 0; i < newVerts.Count; i++)
            {
                if (newVerts[i].notNull)
                {
                    verts.Add(newVerts[i].Point);
                }
            }
        }

        //Test if two Vector3's are nearly equivalent
        bool VertsAreEqual(Vector3 vert1, Vector3 vert2)
        {
            return Mathf.Approximately(vert1.x, vert2.x) &&
                Mathf.Approximately(vert1.y, vert2.y) &&
                Mathf.Approximately(vert1.z, vert2.z);
        }

        //Checks if a vertex index is contained within certain lists of indices
        bool ListsContainVert(int vertIndex, params List<int>[] lists)
        {
            for (int i = 0; i < lists.Length; i++)
            {
                if (lists[i].Contains(vertIndex))
                {
                    return true;
                }
            }
            return false;
        }

        //Creates a list of vertex indices contained in each side of the strip
        List<int> MarkSide(int side)
        {
            List<int> markedVerts = new List<int>();
            switch (side)
            {
                case 0:
                    markedVerts.AddRange(Enumerable.Range(props.cornerDetails[side], props.YZDetail + 2));
                    break;
                case 1:
                    markedVerts.AddRange(Enumerable.Range(props.YZDetail + 1 + props.cornerDetails[side] + props.cornerDetails[side - 1], props.XYDetail + 2));
                    break;
                case 2:
                    markedVerts.AddRange(Enumerable.Range(props.YZDetail + props.XYDetail + 2 + props.cornerDetails[side] + props.cornerDetails[side - 1] + props.cornerDetails[side - 2], props.YZDetail + 2));
                    break;
                case 3:
                    markedVerts.AddRange(Enumerable.Range(props.YZDetail * 2 + props.XYDetail + 3 + props.cornerDetails[side] + props.cornerDetails[side - 1] + props.cornerDetails[side - 2] + props.cornerDetails[side - 3], props.XYDetail + 1));
                    break;
            }

            return markedVerts;
        }

        //Arrange each side of the strip into 4 separate strips
        public void ArrangeSides()
        {
            List<int> side0Mark = MarkSide(0);
            List<int> side1Mark = MarkSide(1);
            List<int> side2Mark = MarkSide(2);
            List<int> side3Mark = MarkSide(3);

            side0.Clear();
            side1.Clear();
            side2.Clear();
            side3.Clear();
            RemoveCornerVerts(side0Mark, side1Mark, side2Mark, side3Mark);

            int side = 0;
            int detailOffset = 0;
            while (side < 4)
            {
                //Add vertices to each side
                if (props.cornerDetails[side] == 0 && side > 0)
                {
                    detailOffset--;
                }
                //Each side has at least 2 vertices, plus the extras added by the detail in between
                switch (side)
                {
                    case 0:
                        side0.AddRange(verts.GetRange(0, props.YZDetail + 2));
                        break;
                    case 1:
                        side1.AddRange(verts.GetRange(props.YZDetail + 2 + detailOffset, props.XYDetail + 2));
                        break;
                    case 2:
                        side2.AddRange(verts.GetRange(props.YZDetail + props.XYDetail + 4 + detailOffset, props.YZDetail + 2));
                        break;
                    case 3:
                        side3.AddRange(verts.GetRange(props.YZDetail * 2 + props.XYDetail + 5 + detailOffset, props.XYDetail + 2));
                        side3.Add(verts[0]);//Extra copy of first vertex at the end of the last side
                        break;
                }
                side++;
            }
        }

        //Used for generating the top and bottom caps of the collision mesh
        public void GenerateCap()
        {
            float smoothness = 1.0f;
            if (props != null)
            {
                smoothness = props.detailSmoothness;
            }

            //Use the vertex counts of two perpendicular sides
            int segments0 = side0.Count;
            int segments1 = side1.Count;
            //Two nested loops for the vertices of each side
            for (int i = 0; i < segments0; i++)
            {
                float ti = 1.0f - (i * 1.0f) / ((segments0 - 1) * 1.0f);
                for (int j = 0; j < segments1; j++)
                {
                    float tj = (j * 1.0f) / ((segments1 - 1) * 1.0f);
                    capVerts.Add(
                        Vector3.Lerp(
                            new Vector3(
                                Mathf.Lerp(side0[i].x, side2[segments0 - i - 1].x, tj),
                                Mathf.Lerp(side0[i].y, side2[segments0 - i - 1].y, Mathf.Lerp(tj, Mathf.Pow(tj, 2.0f - tj * 2.0f), smoothness)),
                                Mathf.Lerp(side0[i].z, side2[segments0 - i - 1].z, tj)
                                ),
                            new Vector3(
                                Mathf.Lerp(side1[j].x, side3[segments1 - j].x, ti),
                                Mathf.Lerp(side1[j].y, side3[segments1 - j].y, Mathf.Lerp(ti, Mathf.Pow(ti, 2.0f - ti * 2.0f), smoothness)),
                                Mathf.Lerp(side1[j].z, side3[segments1 - j].z, ti)
                                ),
                            ti < 0.01f || ti > 0.99f ? 1.0f : (tj < 0.01f || tj > 0.99f ? 0.0f : 0.5f)//Forcing verts at beginning or end to lerp completely to one side or the other, else lerp halfway
                            )
                        );
                }
            }
        }
    }

    //Class for passing vertex strip properties
    public class VertexStripProps
    {
        public bool isTop;//Is this part of the top section?
        public bool flat;//Is this strip meant to be a flat top or bottom? (With zero top or bottom segments)
        public int[] cornerDetails;//Ordered front right, front left, back left, back right
        public int XYDetail;//Intermittent side vertices on X-Y plane
        public int YZDetail;//Intermittent side vertices on Y-Z plane
        //X-Z detail is not relevant because the vertex strips are flat in the X-Z plane; new strips are created for increased detail in this plane
        public float cornerProgress;//Progress for vertical rounding of corners
        public float detailSmoothness;//Smoothness/roundness of extra detail vertices

        public ColliderCorner[] corners;//Reference to corners used to position vertices
    }

    //Alternative to Vector3 that can be set to null
    //Used for removing corner vertices from side strips; they are marked as null
    public class NullableVector3
    {
        public float x = 0.0f;
        public float y = 0.0f;
        public float z = 0.0f;
        public bool notNull = false;
        public Vector3 Point
        {
            get { return new Vector3(x, y, z); }
            set { x = Point.x; y = Point.y; z = Point.z; notNull = true; }
        }

        public NullableVector3(Vector3 setter)
        {
            x = setter.x;
            y = setter.y;
            z = setter.z;
            notNull = true;
        }

        public void Nullify()
        {
            notNull = false;
        }
    }
}