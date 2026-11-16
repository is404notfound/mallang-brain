using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mallang.RR02.Screens
{
    // 뇌 -> 친구로 흐르는 곡선 연결선(2차 베지어)을 리본 메시로 그린다.
    // progress 0..1 로 A쪽부터 자라며, width로 두께, 코어 위에 넓고 옅은 글로우를 깐다.
    // 가구가 아니라 '애니메이션 요소'라 베이크하지 않고 코드로 그린다(원칙 6: 변하는가로 분해).
    public class Rr02CurveLine : Graphic
    {
        public Vector2 p0;           // 시작(뇌)
        public Vector2 p1;           // 제어점(곡률)
        public Vector2 p2;           // 끝(친구)
        public float width = 12f;    // 코어 두께
        public float glowWidth = 26f;// 글로우 두께
        [Range(0f, 1f)] public float progress = 1f;
        public int segments = 28;

        public void SetEndpoints(Vector2 start, Vector2 end, float bow)
        {
            p0 = start; p2 = end;
            // 제어점: 중점에서 위로 bow 만큼 띄워 완만한 아치.
            var mid = (start + end) * 0.5f;
            p1 = mid + new Vector2(0f, bow);
        }

        Vector2 Bezier(float t)
        {
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (progress <= 0.001f) return;
            int segs = Mathf.Max(2, Mathf.CeilToInt(segments * progress));
            float tEnd = progress;

            // 글로우(넓고 옅음) 먼저, 코어(좁고 진함) 나중에 — 같은 메시에 두 리본.
            AddRibbon(vh, segs, tEnd, glowWidth, new Color(color.r, color.g, color.b, color.a * 0.28f));
            AddRibbon(vh, segs, tEnd, width, color);
        }

        void AddRibbon(VertexHelper vh, int segs, float tEnd, float w, Color col)
        {
            int baseIdx = vh.currentVertCount;
            var verts = new List<Vector2>();
            for (int i = 0; i <= segs; i++)
            {
                float t = tEnd * (i / (float)segs);
                verts.Add(Bezier(t));
            }
            for (int i = 0; i < verts.Count; i++)
            {
                Vector2 dir;
                if (i == 0) dir = (verts[1] - verts[0]).normalized;
                else if (i == verts.Count - 1) dir = (verts[i] - verts[i - 1]).normalized;
                else dir = (verts[i + 1] - verts[i - 1]).normalized;
                Vector2 perp = new Vector2(-dir.y, dir.x) * (w * 0.5f);
                var v = UIVertex.simpleVert; v.color = col;
                v.position = verts[i] + perp; vh.AddVert(v);
                v.position = verts[i] - perp; vh.AddVert(v);
            }
            for (int i = 0; i < verts.Count - 1; i++)
            {
                int a = baseIdx + i * 2;
                vh.AddTriangle(a, a + 1, a + 2);
                vh.AddTriangle(a + 1, a + 3, a + 2);
            }
        }
    }
}
