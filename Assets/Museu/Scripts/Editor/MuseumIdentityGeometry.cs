using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lassu.Museum.Editor
{
    /// <summary>Small editor-only mesh authoring kit. One persisted mesh per local group/material.</summary>
    public static class MuseumIdentityGeometry
    {
        public const string MeshPath = "Assets/Museu/Art/DecadeIdentityGeometry.asset";
        private static Mesh library;
        private static readonly Dictionary<string, Mesh> Saved = new Dictionary<string, Mesh>();
        private static readonly Dictionary<Transform, Dictionary<Material, Shape>> Pending = new Dictionary<Transform, Dictionary<Material, Shape>>();

        public static void Begin()
        {
            Pending.Clear(); Saved.Clear();
            library = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (library == null)
            {
                library = new Mesh { name = "DecadeIdentityGeometry" };
                AssetDatabase.CreateAsset(library, MeshPath);
            }
            foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(MeshPath).OfType<Mesh>()) Saved[mesh.name] = mesh;
        }

        private static Shape Get(Transform parent, Material mat)
        {
            if (!Pending.TryGetValue(parent, out var materials)) Pending[parent] = materials = new Dictionary<Material, Shape>();
            if (!materials.TryGetValue(mat, out var shape)) materials[mat] = shape = new Shape();
            return shape;
        }

        public static void Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat, Quaternion? rotation = null)
        {
            var s = Get(parent, mat); var q = rotation ?? Quaternion.identity;
            Vector3 P(float x, float y, float z) => center + q * Vector3.Scale(new Vector3(x,y,z), size * .5f);
            s.Quad(P(-1,-1,-1),P(-1,1,-1),P(1,1,-1),P(1,-1,-1));
            s.Quad(P(1,-1,1),P(1,1,1),P(-1,1,1),P(-1,-1,1));
            s.Quad(P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1));
            s.Quad(P(1,-1,-1),P(1,1,-1),P(1,1,1),P(1,-1,1));
            s.Quad(P(-1,1,-1),P(-1,1,1),P(1,1,1),P(1,1,-1));
            s.Quad(P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1),P(1,-1,1));
        }

        public static void Tube(Transform parent, string name, Vector3[] points, float radius, Material mat, bool closed = false, int sides = 6)
        {
            if (points == null || points.Length < 2) return;
            var s = Get(parent,mat); sides = Mathf.Max(3,sides);
            int count = points.Length; int first = s.V.Count;
            var tangents = new Vector3[count];
            var frames = new Vector3[count];
            for (int i=0;i<count;i++)
            {
                var prev = points[closed ? (i+count-1)%count : Mathf.Max(0,i-1)];
                var next = points[closed ? (i+1)%count : Mathf.Min(count-1,i+1)];
                tangents[i] = (next-prev).normalized;
                if(tangents[i].sqrMagnitude<.1f)tangents[i]=i>0?tangents[i-1]:Vector3.up;
            }
            frames[0]=Vector3.Cross(tangents[0],Mathf.Abs(Vector3.Dot(tangents[0],Vector3.up))>.93f?Vector3.forward:Vector3.up).normalized;
            // Parallel transport avoids abruptly changing the section when a curve turns vertical.
            for(int i=1;i<count;i++)
            {
                var transported=Quaternion.FromToRotation(tangents[i-1],tangents[i])*frames[i-1];
                frames[i]=Vector3.ProjectOnPlane(transported,tangents[i]).normalized;
            }
            float closingTwist=0f;
            if(closed)
            {
                var endFrame=Quaternion.FromToRotation(tangents[count-1],tangents[0])*frames[count-1];
                closingTwist=Vector3.SignedAngle(endFrame,frames[0],tangents[0]);
            }
            for(int i=0;i<count;i++)
            {
                var tangent=tangents[i];
                var across=Quaternion.AngleAxis(closingTwist*i/count,tangent)*frames[i];
                var other = Vector3.Cross(tangent,across).normalized;
                for(int k=0;k<sides;k++)
                {
                    float a=k*Mathf.PI*2f/sides; var n=across*Mathf.Cos(a)+other*Mathf.Sin(a);
                    s.Vertex(points[i]+radius*n,n,new Vector2(i/(float)count,k/(float)sides));
                }
            }
            int segments=closed?count:count-1;
            for(int i=0;i<segments;i++) for(int k=0;k<sides;k++)
            {
                int a=first+i*sides+k,b=first+i*sides+(k+1)%sides;
                int c=first+((i+1)%count)*sides+(k+1)%sides,d=first+((i+1)%count)*sides+k;
                s.Indices(a,b,c,d);
            }
            if(!closed)
            {
                for(int end=0;end<2;end++)
                {
                    int p=end==0?0:count-1; var n=(points[p]-points[end==0?1:count-2]).normalized;
                    for(int k=0;k<sides;k++)
                    {
                        var a=s.V[first+p*sides+k];var b=s.V[first+p*sides+(k+1)%sides];
                        if(end==0)s.Triangle(points[p],b,a,n);else s.Triangle(points[p],a,b,n);
                    }
                }
            }
        }

        public static void Disc(Transform parent, string name, Vector3 center, float radius, float depth, Material mat, Quaternion? rotation = null)
        {
            var s=Get(parent,mat); var q=rotation??Quaternion.identity; const int sides=24;
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                Vector3 P(float angle,float z)=>center+q*new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,z);
                var p=P(a,-depth/2);var r=P(b,-depth/2);var t=P(b,depth/2);var u=P(a,depth/2);
                s.Quad(p,r,t,u);
                s.Triangle(center+q*(Vector3.back*depth/2),r,p,q*Vector3.back);
                s.Triangle(center+q*(Vector3.forward*depth/2),u,t,q*Vector3.forward);
            }
        }

        public static void Flush()
        {
            foreach(var group in Pending)
            foreach(var item in group.Value)
            {
                string key="Identity_"+Hierarchy(group.Key)+"_"+item.Key.name;
                if(!Saved.TryGetValue(key,out var mesh))
                {
                    mesh=new Mesh{name=key};AssetDatabase.AddObjectToAsset(mesh,library);Saved[key]=mesh;
                }
                var s=item.Value;mesh.Clear();mesh.indexFormat=s.V.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
                mesh.SetVertices(s.V);mesh.SetNormals(s.N);mesh.SetUVs(0,s.U);mesh.SetTriangles(s.T,0);mesh.RecalculateBounds();mesh.RecalculateTangents();
                EditorUtility.SetDirty(mesh);
                var obj=new GameObject(group.Key.name+"_"+item.Key.name,typeof(MeshFilter),typeof(MeshRenderer));
                obj.transform.SetParent(group.Key,false);obj.GetComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=obj.GetComponent<MeshRenderer>();renderer.sharedMaterial=item.Key;
                renderer.shadowCastingMode=group.Key.name.EndsWith("Inlays",StringComparison.Ordinal)?ShadowCastingMode.Off:ShadowCastingMode.On;
                renderer.receiveShadows=true;renderer.allowOcclusionWhenDynamic=true;
                GameObjectUtility.SetStaticEditorFlags(obj,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccludeeStatic|StaticEditorFlags.ReflectionProbeStatic);
            }
            Pending.Clear();
        }

        private static string Hierarchy(Transform t)
        {
            string path=t.name;while(t.parent!=null&&t.parent.name!=MuseumArtisticArchitecture.RootName){t=t.parent;path=t.name+"_"+path;}return path;
        }

        private sealed class Shape
        {
            public readonly List<Vector3> V=new List<Vector3>(),N=new List<Vector3>();
            public readonly List<Vector2> U=new List<Vector2>();public readonly List<int>T=new List<int>();
            public void Vertex(Vector3 v,Vector3 n,Vector2 uv){V.Add(v);N.Add(n);U.Add(uv);}
            public void Indices(int a,int b,int c,int d){T.Add(a);T.Add(b);T.Add(c);T.Add(a);T.Add(c);T.Add(d);}
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                int i=V.Count;var n=Vector3.Cross(b-a,c-a).normalized;
                Vertex(a,n,Vector2.zero);Vertex(b,n,Vector2.up);Vertex(c,n,Vector2.one);Vertex(d,n,Vector2.right);Indices(i,i+1,i+2,i+3);
            }
            public void Triangle(Vector3 a,Vector3 b,Vector3 c,Vector3 normal)
            {int i=V.Count;Vertex(a,normal,Vector2.zero);Vertex(b,normal,Vector2.right);Vertex(c,normal,Vector2.up);T.Add(i);T.Add(i+1);T.Add(i+2);}
        }
    }
}
