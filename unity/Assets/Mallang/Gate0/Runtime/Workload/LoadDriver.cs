using System.Collections.Generic;
using Mallang.Gate0.Runtime.Configuration;
using UnityEngine;

namespace Mallang.Gate0.Runtime.Workload
{
    // Realises a WorkloadSchedule as an actual scene load: a crowd of animated sprite proxies plus per-beat
    // one-shot VFX and elimination waves. This is the upper-bound proxy load for Gate 0 measurement, not the
    // final art. Counts are deterministic; visuals are throwaway generated sprites so no art assets are needed.
    // The real per-frame cost (Instantiate/Destroy, transform animation, extra draw layers) is what the frame
    // metrics measure. Character/marimba/contest visuals come after both platforms pass technical validation.
    public sealed class LoadDriver : MonoBehaviour
    {
        [Range(1, 100)] public int spriteLayers = 2;

        // Assigned by the scene setup script from Assets/Mallang/Gate0/Settings/CrowdProxy.controller. When
        // set, each crowd member gets a real Animator (genuine Mecanim overhead, matching the scenario's
        // "animatorCount") instead of a manual Transform rotation in Update.
        public RuntimeAnimatorController animatorController;

        private WorkloadSchedule schedule;
        private readonly List<GameObject> crowd = new List<GameObject>();
        private readonly List<GameObject> liveVfx = new List<GameObject>();
        private Sprite proxySprite;
        private int nextEliminationTarget;

        public int ActiveCrowd => crowd.Count;
        public long VfxSpawnedTotal { get; private set; }
        public long EliminatedTotal { get; private set; }
        public string PhaseId => schedule?.PhaseId;
        public int BeatCount => schedule?.BeatCount ?? 0;

        // Build the crowd for one phase. Idempotent: clears any previous phase first.
        public void BeginPhase(Gate0Scenario.Phase phase)
        {
            Teardown();
            schedule = new WorkloadSchedule(phase);
            if (proxySprite == null) proxySprite = CreateProxySprite();
            for (var i = 0; i < schedule.CrowdCount; i++) crowd.Add(CreateCrowdMember(i));
        }

        // Clears the crowd/VFX without starting a new phase. Call when a phase ends so the next measurement
        // starts from an empty scene.
        public void EndPhase()
        {
            Teardown();
            schedule = null;
        }

        // Apply one beat of scheduled load. Call once per musical beat while Playing.
        public void Tick(int beat)
        {
            if (schedule == null) return;
            var eliminations = schedule.EliminationsOnBeat(beat);
            for (var i = 0; i < eliminations && crowd.Count > 0; i++)
            {
                var index = nextEliminationTarget % crowd.Count;
                var member = crowd[index];
                crowd.RemoveAt(index);
                Destroy(member);
                EliminatedTotal++;
                nextEliminationTarget++;
                crowd.Add(CreateCrowdMember(crowd.Count)); // respawn to sustain the worst-case count
            }
            var vfx = schedule.VfxOnBeat(beat);
            for (var i = 0; i < vfx; i++) { liveVfx.Add(CreateVfx(i)); VfxSpawnedTotal++; }
        }

        private void Update()
        {
            // When no shared AnimatorController asset is available (e.g. it was never generated), fall back to
            // a manual rotation so the crowd still moves and still carries real per-frame Transform work.
            if (animatorController == null)
            {
                var t = Time.time;
                for (var i = 0; i < crowd.Count; i++)
                {
                    var tr = crowd[i].transform;
                    tr.localRotation = Quaternion.Euler(0, 0, (t * 90f + i) % 360f);
                }
            }
            for (var i = liveVfx.Count - 1; i >= 0; i--)
            {
                var go = liveVfx[i];
                if (go == null) { liveVfx.RemoveAt(i); continue; }
                var s = go.transform.localScale.x + Time.deltaTime * 4f;
                if (s > 2f) { Destroy(go); liveVfx.RemoveAt(i); }
                else go.transform.localScale = new Vector3(s, s, 1);
            }
        }

        private GameObject CreateCrowdMember(int index)
        {
            var member = new GameObject("crowd-" + index);
            member.transform.SetParent(transform, false);
            member.transform.localPosition = new Vector3((index % 20) - 10, (index / 20) - 3, 0);
            if (animatorController != null)
            {
                var animator = member.AddComponent<Animator>();
                animator.runtimeAnimatorController = animatorController;
                // Stagger playback so 30-100 members don't all evaluate on the exact same frame slice.
                animator.Play(0, 0, (index % 20) / 20f);
            }
            for (var layer = 0; layer < spriteLayers; layer++)
            {
                var layerGo = new GameObject("layer-" + layer);
                layerGo.transform.SetParent(member.transform, false);
                var sr = layerGo.AddComponent<SpriteRenderer>();
                sr.sprite = proxySprite;
                sr.color = layer == 0 ? new Color(0.3f, 0.6f, 0.9f, 1f) : new Color(1f, 1f, 1f, 0.4f);
                sr.sortingOrder = layer;
            }
            return member;
        }

        private GameObject CreateVfx(int index)
        {
            var go = new GameObject("vfx");
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(0.2f, 0.2f, 1);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = proxySprite;
            sr.color = new Color(1f, 0.85f, 0.3f, 0.8f);
            sr.sortingOrder = 100;
            return go;
        }

        private static Sprite CreateProxySprite()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var px = new Color32(255, 255, 255, 255);
            tex.SetPixels32(new[] { px, px, px, px });
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2);
        }

        private void Teardown()
        {
            foreach (var go in crowd) if (go != null) Destroy(go);
            foreach (var go in liveVfx) if (go != null) Destroy(go);
            crowd.Clear();
            liveVfx.Clear();
            nextEliminationTarget = 0;
        }

        private void OnDestroy()
        {
            Teardown();
            if (proxySprite != null) { Destroy(proxySprite.texture); Destroy(proxySprite); }
        }
    }
}
