using System.Collections.Generic;

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
        public static IReadOnlyList<LeaderEntry> Population =>
            throw new System.NotImplementedException();

        public static int RankForScore(int score) =>
            throw new System.NotImplementedException();

        public static LeaderboardBoard Build(int myScore, string myName = "나", int myAvatar = 0) =>
            throw new System.NotImplementedException();
    }
}
