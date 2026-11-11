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

        private int _seed;

        private const int MelodyLength = 8;

        private static readonly Rr02Friend[] AllFriends =
        {
            Rr02Friend.Rabbit, Rr02Friend.Capybara, Rr02Friend.Fox, Rr02Friend.Squirrel
        };

        // 난이도 스케줄을 따라 시드 기반 절차 생성 차트를 만든다. 라운드마다 독립된 System.Random을 쓴다.
        public static Rr02Chart Generate(int seed)
        {
            var schedule = BuildSchedule();
            var rounds = new Rr02Round[schedule.Count];
            for (int r = 0; r < schedule.Count; r++)
            {
                var rng = new System.Random(unchecked(seed * 1000003 + r * 9176 + 17));
                rounds[r] = GenerateRound(r + 1, schedule[r].variety, schedule[r].hidden, rng);
            }

            return new Rr02Chart { Rounds = rounds, _seed = seed };
        }

        // 난이도 램프: hidden 0~3 각 블록 안에서 variety(=n) 1~4가 n번씩 반복된다 → 총 40라운드.
        public static List<(int variety, int hidden)> BuildSchedule()
        {
            var list = new List<(int variety, int hidden)>();
            for (int h = 0; h <= 3; h++)
                for (int n = 1; n <= 4; n++)
                    for (int rep = 0; rep < n; rep++)
                        list.Add((n, h));
            return list;
        }

        private static Rr02Round GenerateRound(int number, int variety, int hidden, System.Random rng)
        {
            variety = Math.Max(1, Math.Min(AllFriends.Length, variety)); // 동물은 최대 4종
            hidden = Math.Max(0, Math.Min(MelodyLength, hidden));

            var chosen = (Rr02Friend[])AllFriends.Clone();
            Shuffle(chosen, rng);

            var slots = new Rr02Friend[MelodyLength];
            for (int i = 0; i < variety; i++)
                slots[i] = chosen[i];
            for (int i = variety; i < MelodyLength; i++)
                slots[i] = chosen[rng.Next(variety)];
            Shuffle(slots, rng);

            var notes = new Rr02Note[MelodyLength];
            for (int i = 0; i < MelodyLength; i++)
            {
                long targetUs = (long)i * Rr02Rules.BeatUs;
                notes[i] = new Rr02Note(i, slots[i], targetUs);
            }

            var indices = new List<int>();
            for (int i = 0; i < MelodyLength; i++) indices.Add(i);
            Shuffle(indices, rng);
            var hiddenList = indices.GetRange(0, hidden);
            hiddenList.Sort();

            return new Rr02Round
            {
                Number = number,
                Notes = notes,
                HiddenIndices = hiddenList.ToArray()
            };
        }

        private static void Shuffle<T>(IList<T> a, System.Random rng)
        {
            for (int i = a.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (a[i], a[j]) = (a[j], a[i]);
            }
        }

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

        // k번째 무한 구절은 매번 새로 생성하는 최대 난이도(variety 4) 멜로디다. 전 칸이 숨겨진 채로 시작한다(AC-013).
        public Rr02Round InfinitePhrase(int k)
        {
            if (k < 1)
                throw new ArgumentException($"k는 1 이상이어야 합니다 (k={k}).", nameof(k));

            var rng = new System.Random(unchecked(_seed * 2000003 + k * 31337 + 7));
            return GenerateRound(1000 + k, 4, MelodyLength, rng);
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
