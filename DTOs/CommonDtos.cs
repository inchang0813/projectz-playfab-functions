#nullable enable
using System;

namespace AfterHuman.Games.Function.DTOs
{
    #region Common Data Classes

    /// <summary>
    /// 보상 아이템 정보
    /// 서버: AfterHuman.Games.Function.DTOs.RewardItem
    /// </summary>
    [Serializable]
    public class RewardItem
    {
        public string? itemId { get; set; }
        public int amount { get; set; }
        public string? displayName { get; set; }
    }

    /// <summary>
    /// 파밍한 아이템 정보 (클라이언트 → 서버 전송용)
    /// 서버: AfterHuman.Games.Function.DTOs.LootedItemData
    /// </summary>
    [Serializable]
    public class LootedItemData
    {
        public string? itemId { get; set; }
        public int amount { get; set; }
        public string? containerId { get; set; }
    }

    #endregion
}