using System;
using System.Collections.Generic;
using System.Text;
using Mallang.Gate0.Runtime.Input;
using UnityEngine;

namespace Mallang.Gate0.Runtime.Measurement
{
    // Human-readable raw samples for one attempt. The RunRecord contains summaries; this trace preserves the
    // individual clock, frame, beat, and input observations needed to recalculate those summaries later.
    [Serializable]
    public sealed class RawMeasurementTrace
    {
        public const int CurrentSchemaVersion = 1;

        [Serializable] public sealed class ClockSample
        {
            public double realtimeSeconds;
            public double dspSeconds;
        }

        [Serializable] public sealed class FrameSample
        {
            public long phaseElapsedUs;
            public int deltaUs;
        }

        [Serializable] public sealed class BeatSample
        {
            public int beatIndex;
            public long phaseElapsedUs;
        }

        [Serializable] public sealed class InputSample
        {
            public string source;
            public bool leftHand;
            public double eventRealtimeSeconds;
            public double convertedDspSeconds;
            public long phaseInputUs;
            public bool accepted;
            public string band;
            public long offsetUs;
            public int cueIndex;
            public string rejectReason;
        }

        public int schemaVersion = CurrentSchemaVersion;
        public string attemptId;
        public string phaseId;
        public List<ClockSample> clockSamples = new List<ClockSample>();
        public List<FrameSample> frameSamples = new List<FrameSample>();
        public List<BeatSample> beatSamples = new List<BeatSample>();
        public List<InputSample> inputSamples = new List<InputSample>();

        public RawMeasurementTrace(string phaseId)
        {
            attemptId = Guid.NewGuid().ToString("N");
            this.phaseId = phaseId;
        }

        public void AddClock(double realtimeSeconds, double dspSeconds) => clockSamples.Add(new ClockSample
        {
            realtimeSeconds = realtimeSeconds,
            dspSeconds = dspSeconds,
        });

        public void AddFrame(long phaseElapsedUs, double deltaSeconds) => frameSamples.Add(new FrameSample
        {
            phaseElapsedUs = phaseElapsedUs,
            deltaUs = Mathf.Max(0, Mathf.RoundToInt((float)(deltaSeconds * 1_000_000.0))),
        });

        public void AddBeat(int beatIndex, long phaseElapsedUs) => beatSamples.Add(new BeatSample
        {
            beatIndex = beatIndex,
            phaseElapsedUs = phaseElapsedUs,
        });

        public void AddInput(string source, bool leftHand, double realtimeSeconds, double dspSeconds,
            long phaseInputUs, RawTimingProbe.Result result) => inputSamples.Add(new InputSample
        {
            source = source,
            leftHand = leftHand,
            eventRealtimeSeconds = realtimeSeconds,
            convertedDspSeconds = dspSeconds,
            phaseInputUs = phaseInputUs,
            accepted = result.Accepted,
            band = result.Band.ToString(),
            offsetUs = result.OffsetUs,
            cueIndex = result.CueIndex,
            rejectReason = result.RejectReason,
        });

        public byte[] ToUtf8Json() => Encoding.UTF8.GetBytes(JsonUtility.ToJson(this, true));
    }
}
