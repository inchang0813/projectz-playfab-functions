using System.Text.Json;

namespace AfterHuman.Games.Function.Models;

/// <summary>
/// PlayFab ExecuteFunction 요청 포맷 (필요 필드만 최소 정의)
/// </summary>
public class PlayFabFunctionRequest
{
    public JsonElement FunctionArgument { get; set; }
    public CallerEntityProfile? CallerEntityProfile { get; set; }
}

public class CallerEntityProfile
{
    public Lineage? Lineage { get; set; }
}

public class Lineage
{
    public string? MasterPlayerAccountId { get; set; }
}

/// <summary>
/// 보상 아이템
/// </summary>
public class RewardItem
{
    public string itemId { get; set; } = string.Empty;
    public int amount { get; set; }
    public string? displayName { get; set; }
}
