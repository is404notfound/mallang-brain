using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mallang.RR02
{
    public sealed class Rr02Round
    {
        public int Number;
        public IReadOnlyList<Rr02Note> Notes;
        public IReadOnlyList<int> HiddenIndices;
    }

    public sealed class Rr02Chart
    {
        public IReadOnlyList<Rr02Round> Rounds;

        // four-friend-chart.json(패키지 원본 그대로)을 파싱한다. 형식이 어긋나면 조용한 기본값 대신 예외.
        public static Rr02Chart LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("차트 JSON이 비어 있습니다.", nameof(json));

            ChartDto dto;
            try
            {
                dto = JsonUtility.FromJson<ChartDto>(json);
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new ArgumentException("차트 JSON을 해석할 수 없습니다.", e);
            }

            if (dto == null || dto.rounds == null || dto.rounds.Length != RoundCount)
                throw new ArgumentException($"차트에는 정확히 {RoundCount}개 라운드가 있어야 합니다.");

            var rounds = new Rr02Round[RoundCount];
            for (int r = 0; r < RoundCount; r++)
            {
                var dtoRound = dto.rounds[r];
                if (dtoRound == null || dtoRound.steps == null || dtoRound.steps.Length == 0)
                    throw new ArgumentException($"라운드 {r + 1}에 step이 없습니다.");

                // 각 라운드는 step이 하나뿐이다(설계 5.4). 그 step의 값만 사용한다.
                var step = dtoRound.steps[0];
                if (step == null || step.sequence == null || step.targetTicks == null)
                    throw new ArgumentException($"라운드 {r + 1}의 step에 sequence/targetTicks가 없습니다.");
                if (step.sequence.Length != step.targetTicks.Length)
                    throw new ArgumentException($"라운드 {r + 1}: sequence와 targetTicks 길이가 일치하지 않습니다.");

                var notes = new Rr02Note[step.sequence.Length];
                for (int i = 0; i < notes.Length; i++)
                {
                    // 1박 = 480 tick = 400,000µs(FR-007). 전부 long 산수로 계산한다.
                    var targetUs = (long)step.targetTicks[i] * Rr02Rules.BeatUs / Rr02Rules.TicksPerBeat;
                    notes[i] = new Rr02Note(i, ToFriend(step.sequence[i], r + 1, i), targetUs);
                }

                // hiddenIndices가 생략된 라운드(R1)는 숨김 없음으로 본다.
                var hidden = step.hiddenIndices ?? Array.Empty<int>();
                rounds[r] = new Rr02Round
                {
                    Number = r + 1,
                    Notes = notes,
                    HiddenIndices = hidden
                };
            }

            return new Rr02Chart { Rounds = rounds };
        }

        // k=1→R2 … k=8→R9, k=9→다시 R2 순환. HiddenIndices는 0~7 전부. R1 두 소리 패턴은 쓰지 않는다.
        public Rr02Round InfinitePhrase(int k)
        {
            if (k < 1)
                throw new ArgumentException($"k는 1 이상이어야 합니다 (k={k}).", nameof(k));

            var source = Rounds[(k - 1) % (Rounds.Count - 1) + 1];

            // 무한 모드에서는 입력되는 칸(0~7)이 전부 숨겨진 채로 시작한다(AC-013).
            var allHidden = new int[source.Notes.Count];
            for (int i = 0; i < allHidden.Length; i++)
                allHidden[i] = i;

            return new Rr02Round
            {
                Number = source.Number,
                Notes = source.Notes,
                HiddenIndices = allHidden
            };
        }

        // JsonUtility는 최상위 배열이나 중첩 제네릭 안의 List<T]를 안정적으로 읽지 못하므로
        // DTO는 배열(string[]/int[])로 받는다.
        [Serializable]
        private sealed class ChartDto
        {
            public int rulesVersion;
            public int bpm;
            public int ticksPerBeat;
            public RoundDto[] rounds;
        }

        [Serializable]
        private sealed class RoundDto
        {
            public int round;
            public int hiddenCount;
            public string[] sequence;
            public StepDto[] steps;
        }

        [Serializable]
        private sealed class StepDto
        {
            public int length;
            public string[] sequence;
            public int[] hiddenIndices;
            public int[] targetTicks;
        }

        private const int RoundCount = 9; // FR-011: 일반 라운드는 R1~R9 고정

        private static Rr02Friend ToFriend(string id, int roundNumber, int index)
        {
            switch (id)
            {
                case "rabbit": return Rr02Friend.Rabbit;
                case "capybara": return Rr02Friend.Capybara;
                case "fox": return Rr02Friend.Fox;
                case "squirrel": return Rr02Friend.Squirrel;
                default: throw new ArgumentException($"라운드 {roundNumber} note {index}: 알 수 없는 친구 id '{id}'.");
            }
        }
    }

    // 구절 시작 절대시각의 단일 진실(stage-review 결정 3). 장면 코드는 이 계산기만 사용한다 (필수).
    public static class Rr02Schedule
    {
        public const long CountInUs = 4800000; // 12박

        private const long RoundSpanUs = 8000000L;   // 한 라운드 20박
        private const long RepeatOffsetUs = 3200000L; // 듣기→따라치기 8박

        public static long ListenStartUs(int round)
        {
            if (round < 1)
                throw new ArgumentException($"round는 1 이상이어야 합니다 (round={round}).", nameof(round));
            return CountInUs + (round - 1) * RoundSpanUs;
        }

        public static long RepeatStartUs(int round) => ListenStartUs(round) + RepeatOffsetUs;

        public static long RoundEndUs(int round) => ListenStartUs(round) + RoundSpanUs;
    }
}
