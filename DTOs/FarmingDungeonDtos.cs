using System.Collections.Generic;
using AfterHuman.Games.Function.Models;

namespace AfterHuman.Games.Function.DTOs;

public class StartRunFarmingDungeonRequest
{
    public string? dungeonId { get; set; }
}

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

public class EndRunFarmingDungeonRequest
{
    public string runId { get; set; } = string.Empty;
    public bool success { get; set; }
    public int clearTimeSec { get; set; }
}

public class EndRunFarmingDungeonResponse
{
    public bool ok { get; set; }
    public string? message { get; set; }
    public List<RewardItem> rewards { get; set; } = new();
}

