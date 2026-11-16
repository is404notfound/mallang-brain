using System.Collections.Generic;
using System.Linq;

namespace Mallang.RR02
{
    // 온라인 리더보드 표시용 한 행. Avatar: 0 토끼 / 1 카피바라 / 2 여우 / 3 다람쥐.
    public struct LeaderEntry
    {
        public int Rank;
        public string Name;
        public int Score;
        public int Avatar;
        public bool IsMe;
    }

    // 리더보드 화면이 그릴 뷰 모델. Top은 보통 상위 10행,
    // ShowEllipsis는 내 등수가 상위권 밖일 때 ⋯ 표시, Me는 내 행(항상 채움).
    public struct LeaderboardBoard
    {
        public List<LeaderEntry> Top;
        public bool ShowEllipsis;
        public bool MeInTop;
        public LeaderEntry Me;
    }

    // 서버 전환 전 프로토타입용 목(mock) 리더보드. 실제 플레이어 데이터는
    // 출시 때 BaaS 리더보드로 교체한다(FakeRankingServer와 같은 이음새).
    public static class MockLeaderboard
    {
        private static IReadOnlyList<LeaderEntry> _population;

        public static IReadOnlyList<LeaderEntry> Population
        {
            get
            {
                if (_population == null)
                {
                    _population = new List<LeaderEntry>
                    {
                        new LeaderEntry { Rank = 1, Name = "토끼대장", Score = 9870, Avatar = 0, IsMe = false },
                        new LeaderEntry { Rank = 2, Name = "둠칫왕", Score = 9540, Avatar = 1, IsMe = false },
                        new LeaderEntry { Rank = 3, Name = "합주귀신", Score = 9120, Avatar = 2, IsMe = false },
                        new LeaderEntry { Rank = 4, Name = "말랑지휘자", Score = 8860, Avatar = 3, IsMe = false },
                        new LeaderEntry { Rank = 5, Name = "리듬여우", Score = 8420, Avatar = 0, IsMe = false },
                        new LeaderEntry { Rank = 6, Name = "밤의악단", Score = 7950, Avatar = 1, IsMe = false },
                        new LeaderEntry { Rank = 7, Name = "도토리박자", Score = 7610, Avatar = 2, IsMe = false },
                        new LeaderEntry { Rank = 8, Name = "북치는친구", Score = 7280, Avatar = 3, IsMe = false },
                        new LeaderEntry { Rank = 9, Name = "반짝멜로디", Score = 6930, Avatar = 0, IsMe = false },
                        new LeaderEntry { Rank = 10, Name = "캠퍼스스타", Score = 6580, Avatar = 1, IsMe = false }
                    }.AsReadOnly();
                }
                return _population;
            }
        }

        public static int RankForScore(int score)
        {
            var aboveCount = Population.Count(e => e.Score > score);

            if (score >= 6580)
            {
                return aboveCount + 1;
            }
            else
            {
                return 11 + UnityEngine.Mathf.RoundToInt((6580f - score) * (1223f / 2130f));
            }
        }

        public static LeaderboardBoard Build(int myScore, string myName = "나", int myAvatar = 0)
        {
            int myRank = RankForScore(myScore);
            var me = new LeaderEntry
            {
                Rank = myRank,
                Name = myName,
                Score = myScore,
                Avatar = myAvatar,
                IsMe = true
            };

            if (myRank <= 10)
            {
                // I'm in the visible top
                var merged = new List<LeaderEntry>(Population);
                merged.Add(me);

                // Sort by Score descending
                merged = merged.OrderByDescending(e => e.Score).ToList();

                // Take first 10 and reassign ranks
                var top = merged.Take(10).ToList();
                for (int i = 0; i < top.Count; i++)
                {
                    top[i] = new LeaderEntry
                    {
                        Rank = i + 1,
                        Name = top[i].Name,
                        Score = top[i].Score,
                        Avatar = top[i].Avatar,
                        IsMe = top[i].IsMe
                    };
                }

                var meInTop = top.First(e => e.IsMe);

                return new LeaderboardBoard
                {
                    Top = top,
                    MeInTop = true,
                    ShowEllipsis = false,
                    Me = meInTop
                };
            }
            else
            {
                // I'm below the visible top
                return new LeaderboardBoard
                {
                    Top = new List<LeaderEntry>(Population),
                    MeInTop = false,
                    ShowEllipsis = true,
                    Me = me
                };
            }
        }
    }
}
