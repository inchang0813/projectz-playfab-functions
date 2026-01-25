#nullable enable
using System;
using System.Collections.Generic;

namespace AfterHuman.Games.Function.DTOs
{
    // ⚠️ 주의: RewardItem, LootedItemData는 CommonDtos.cs에 정의되어 있습니다.
    // 중복 방지를 위해 이 파일에는 정의하지 않습니다.
    #region StartRun_FarmingDungeon

    /// <summary>
    /// StartRun_FarmingDungeon 요청
    /// 서버: AfterHuman.Games.Function.DTOs.StartRunFarmingDungeonRequest
    /// </summary>
    [Serializable]
    public class StartRunFarmingDungeonRequest
    {
        public string? dungeonId { get; set; }
    }

    /// <summary>
    /// StartRun_FarmingDungeon 응답
    /// 서버: AfterHuman.Games.Function.DTOs.StartRunFarmingDungeonResponse
    /// </summary>
    [Serializable]
    public class StartRunFarmingDungeonResponse
    {
        public bool ok { get; set; }
        public string? runId { get; set; }
        public int seed { get; set; }
        public long serverTime { get; set; }
        public string? message { get; set; }
        public string? dungeonId { get; set; }
        public int runDurationSec { get; set; }
    }

    #endregion

    #region EndRun_FarmingDungeon

    /// <summary>
    /// EndRun_FarmingDungeon 요청
    /// 서버: AfterHuman.Games.Function.DTOs.EndRunFarmingDungeonRequest
    /// </summary>
    [Serializable]
    public class EndRunFarmingDungeonRequest
    {
        public string runId { get; set; } = string.Empty;
        public bool success { get; set; }
        public int clearTimeSec { get; set; }
        public List<LootedItemData> lootedItems { get; set; } = new List<LootedItemData>();
    }

    /// <summary>
    /// EndRun_FarmingDungeon 응답
    /// 서버: AfterHuman.Games.Function.DTOs.EndRunFarmingDungeonResponse
    /// </summary>
    [Serializable]
    public class EndRunFarmingDungeonResponse
    {
        public bool ok { get; set; }
        public string? message { get; set; }
        public List<RewardItem> rewards { get; set; } = new List<RewardItem>();
    }

    #endregion
}