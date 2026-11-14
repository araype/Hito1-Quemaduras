using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles
{
    // Marco de una mano del paciente en el espacio de la malla de la piel (en
    // la pose de enlace, que es donde estan los vertices de las capas de
    // HandSkinMesh): centro y tres ejes sacados de la forma de la mano
    // (componentes principales de sus vertices):
    //   Long:  de la muneca a la punta de los dedos (el mas largo);
    //   Wide:  de un lado al otro de la palma;
    //   Thick: de la palma al dorso (el mas corto).
    // Convierte puntos y direcciones del mundo a ese espacio con el hueso de
    // la mano, asi sirve aunque la mano este girada.
    public class HandFrame
    {
        public SkinnedMeshRenderer Skin { get; private set; }
        public Transform Bone { get; private set; }
        public Vector3 Center { get; private set; }
        public Vector3 Long { get; private set; }
        public Vector3 Wide { get; private set; }
        public Vector3 Thick { get; private set; }

        // Medio largo y medio ancho de la mano (unidades de la malla).
        public float HalfLength { get; private set; }
        public float HalfWidth { get; private set; }

        private Matrix4x4 boneToBind;

        public static HandFrame Build(Component owner, bool rightHand, float minHandWeight, string skinRendererName)
        {
            SkinnedMeshRenderer skin = null;
            foreach (SkinnedMeshRenderer r in owner.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name.Contains(skinRendererName))
                {
                    skin = r;
                    break;
                }
            }

            if (skin == null || skin.sharedMesh == null || !skin.sharedMesh.isReadable)
            {
                Debug.LogWarning("[HandFrame] No se puede leer la piel del paciente.", owner);
                return null;
            }

            string side = rightHand ? "RightHand" : "LeftHand";
            Transform[] bones = skin.bones;
            bool[] handBone = new bool[bones.Length];
            int main = -1;
            for (int i = 0; i < bones.Length; i++)
            {
                handBone[i] = bones[i] != null && bones[i].name.Contains(side);
                if (bones[i] != null && (bones[i].name.EndsWith(side) || (main < 0 && handBone[i])))
                {
                    main = i;
                }
            }

            Mesh mesh = skin.sharedMesh;
            Matrix4x4[] bindposes = mesh.bindposes;
            if (main < 0 || main >= bindposes.Length)
            {
                Debug.LogWarning("[HandFrame] No se encontro el hueso de la mano (" + side + ").", owner);
                return null;
            }

            Vector3[] vertices = mesh.vertices;
            BoneWeight[] weights = mesh.boneWeights;
            if (weights.Length != vertices.Length)
            {
                return null;
            }

            List<Vector3> points = new List<Vector3>();
            for (int v = 0; v < vertices.Length; v++)
            {
                BoneWeight w = weights[v];
                float sum = W(handBone, w.boneIndex0, w.weight0) + W(handBone, w.boneIndex1, w.weight1)
                    + W(handBone, w.boneIndex2, w.weight2) + W(handBone, w.boneIndex3, w.weight3);
                if (sum >= minHandWeight)
                {
                    points.Add(vertices[v]);
                }
            }

            if (points.Count < 10)
            {
                Debug.LogWarning("[HandFrame] La mano tiene muy pocos vertices.", owner);
                return null;
            }

            Vector3 center = Vector3.zero;
            foreach (Vector3 p in points)
            {
                center += p;
            }

            center /= points.Count;

            // Covarianza y sus ejes principales.
            float[,] c = new float[3, 3];
            foreach (Vector3 p in points)
            {
                Vector3 d = p - center;
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        c[i, j] += d[i] * d[j];
                    }
                }
            }

            Vector3[] axes = Eigen(c);

            HandFrame frame = new HandFrame
            {
                Skin = skin,
                Bone = bones[main],
                Long = axes[0],
                Wide = axes[1],
                Thick = Vector3.Cross(axes[0], axes[1]).normalized,
                boneToBind = bindposes[main].inverse
            };

            // Centro y medidas segun la caja de la mano en esos ejes.
            Vector3 min = Vector3.one * float.MaxValue;
            Vector3 max = Vector3.one * float.MinValue;
            foreach (Vector3 p in points)
            {
                Vector3 d = p - center;
                Vector3 q = new Vector3(Vector3.Dot(d, frame.Long), Vector3.Dot(d, frame.Wide), Vector3.Dot(d, frame.Thick));
                min = Vector3.Min(min, q);
                max = Vector3.Max(max, q);
            }

            Vector3 mid = (min + max) * 0.5f;
            frame.Center = center + frame.Long * mid.x + frame.Wide * mid.y + frame.Thick * mid.z;
            frame.HalfLength = (max.x - min.x) * 0.5f;
            frame.HalfWidth = (max.y - min.y) * 0.5f;
            return frame;
        }

        private static float W(bool[] handBone, int index, float weight)
        {
            return index >= 0 && index < handBone.Length && handBone[index] ? weight : 0f;
        }

        // Del mundo al espacio de la malla en la pose de enlace.
        public Matrix4x4 WorldToBind
        {
            get { return boneToBind * Bone.worldToLocalMatrix; }
        }

        public Vector3 ToBind(Vector3 world)
        {
            return WorldToBind.MultiplyPoint3x4(world);
        }

        public Vector3 DirToBind(Vector3 worldDirection)
        {
            return WorldToBind.MultiplyVector(worldDirection).normalized;
        }

        // Cuantas unidades de la malla mide un metro del mundo.
        public float UnitsPerMeter
        {
            get { return WorldToBind.MultiplyVector(Vector3.up).magnitude; }
        }

        // Ejes principales (de mayor a menor varianza) de una matriz simetrica
        // 3x3, con el metodo de Jacobi.
        private static Vector3[] Eigen(float[,] input)
        {
            double[,] a = new double[3, 3];
            double[,] v = { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    a[i, j] = input[i, j];
                }
            }

            for (int sweep = 0; sweep < 30; sweep++)
            {
                double off = a[0, 1] * a[0, 1] + a[0, 2] * a[0, 2] + a[1, 2] * a[1, 2];
                if (off < 1e-18)
                {
                    break;
                }

                for (int p = 0; p < 2; p++)
                {
                    for (int q = p + 1; q < 3; q++)
                    {
                        if (System.Math.Abs(a[p, q]) < 1e-20)
                        {
                            continue;
                        }

                        double theta = (a[q, q] - a[p, p]) / (2.0 * a[p, q]);
                        double t = System.Math.Sign(theta) / (System.Math.Abs(theta) + System.Math.Sqrt(theta * theta + 1.0));
                        if (theta == 0.0)
                        {
                            t = 1.0;
                        }

                        double cs = 1.0 / System.Math.Sqrt(t * t + 1.0);
                        double sn = t * cs;
                        for (int k = 0; k < 3; k++)
                        {
                            double akp = a[k, p], akq = a[k, q];
                            a[k, p] = cs * akp - sn * akq;
                            a[k, q] = sn * akp + cs * akq;
                        }

                        for (int k = 0; k < 3; k++)
                        {
                            double apk = a[p, k], aqk = a[q, k];
                            a[p, k] = cs * apk - sn * aqk;
                            a[q, k] = sn * apk + cs * aqk;
                        }

                        for (int k = 0; k < 3; k++)
                        {
                            double vkp = v[k, p], vkq = v[k, q];
                            v[k, p] = cs * vkp - sn * vkq;
                            v[k, q] = sn * vkp + cs * vkq;
                        }
                    }
                }
            }

            int[] order = { 0, 1, 2 };
            System.Array.Sort(order, (x, y) => a[y, y].CompareTo(a[x, x]));
            Vector3[] axes = new Vector3[3];
            for (int i = 0; i < 3; i++)
            {
                int k = order[i];
                axes[i] = new Vector3((float)v[0, k], (float)v[1, k], (float)v[2, k]).normalized;
            }

            return axes;
        }
    }
}
