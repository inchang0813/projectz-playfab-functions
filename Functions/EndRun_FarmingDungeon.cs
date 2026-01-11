using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AfterHuman.Games.Function;

/// <summary>
/// Farming Dungeon 런 종료 Function
/// - 런 검증 (시간, runId 등)
/// - 보상 계산 및 지급
/// - 통화 지급
/// </summary>
public class EndRun_FarmingDungeon
{
    private readonly ILogger<EndRun_FarmingDungeon> _logger;

    public EndRun_FarmingDungeon(ILogger<EndRun_FarmingDungeon> logger)
    {
        _logger = logger;
    }

    [Function("EndRun_FarmingDungeon")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
    {
        _logger.LogInformation("🏁 EndRun_FarmingDungeon 호출");

        try
        {
            // 요청 파싱
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<EndRunRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null || string.IsNullOrEmpty(request.runId))
            {
                _logger.LogWarning("⚠️ 요청 파싱 실패 또는 runId 누락");
                return new BadRequestObjectResult(new EndRunResponse
                {
                    ok = false,
                    message = "Invalid request: runId is required"
                });
            }

            _logger.LogInformation($"📍 RunId: {request.runId}, Success: {request.success}, Time: {request.clearTimeSec}s");

            // PlayFab Context (추후 추가)
            // var context = await FunctionContext.ParsePlayFabContext(req);
            // var playFabId = context.CallerEntityProfile.Lineage.MasterPlayerAccountId;

            // TODO: Redis/Database에서 런 상태 검증
            // var runState = await GetRunStateAsync(request.runId);
            // if (runState == null) return NotFound("Run not found");
            // if (runState.IsCompleted) return BadRequest("Run already completed");
            
            // ⚠️ 보안: 클라이언트 값 검증
            if (!ValidateRunData(request))
            {
                _logger.LogWarning($"⚠️ 런 검증 실패: {request.runId}");
                return new BadRequestObjectResult(new EndRunResponse
                {
                    ok = false,
                    message = "Run validation failed"
                });
            }

            // 보상 계산 (서버 로직)
            var rewards = CalculateRewards(request);
            var currencies = CalculateCurrencies(request);
            int expGained = CalculateExp(request);

            // TODO: PlayFab API로 실제 보상 지급
            // await GrantRewardsAsync(playFabId, rewards, currencies);

            var response = new EndRunResponse
            {
                ok = true,
                message = request.success ? "Dungeon cleared!" : "Dungeon failed",
                rewards = rewards,
                currencies = currencies,
                expGained = expGained,
                isNewRecord = false // TODO: 기록 비교 로직
            };

            _logger.LogInformation($"✅ 런 종료 성공: {rewards.Count}개 아이템, {currencies.Count}개 통화");
            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ EndRun_FarmingDungeon 실패: {ex.Message}");
            return new ObjectResult(new EndRunResponse
            {
                ok = false,
                message = $"Internal server error: {ex.Message}"
            })
            {
                StatusCode = 500
            };
        }
    }

    #region 검증 로직

    /// <summary>
    /// 런 데이터 검증
    /// </summary>
    private bool ValidateRunData(EndRunRequest request)
    {
        // 시간 검증 (너무 빠른 클리어는 부정)
        if (request.success && request.clearTimeSec < 10)
        {
            _logger.LogWarning($"⚠️ 클리어 시간이 너무 짧음: {request.clearTimeSec}s");
            return false;
        }

        // 최대 시간 초과 검증
        if (request.clearTimeSec > 600) // 10분
        {
            _logger.LogWarning($"⚠️ 최대 시간 초과: {request.clearTimeSec}s");
            return false;
        }

        // TODO: Redis에서 runId 검증
        // - 존재하는 런인가?
        // - 이미 종료된 런인가?
        // - 시작 시간과 종료 시간 차이가 합리적인가?

        return true;
    }

    #endregion

    #region 보상 계산 로직

    /// <summary>
    /// 아이템 보상 계산
    /// </summary>
    private List<RewardItem> CalculateRewards(EndRunRequest request)
    {
        var rewards = new List<RewardItem>();

        if (!request.success)
        {
            // 실패 시 기본 보상만
            rewards.Add(new RewardItem
            {
                itemId = "ITEM_CONSOLATION",
                amount = 1,
                displayName = "위로의 상자"
            });
            return rewards;
        }

        // ⚠️ 실제로는 던전 데이터, 난이도, 클리어 시간 등을 고려해야 함
        // TODO: 던전 보상 테이블 참조

        // 기본 보상
        rewards.Add(new RewardItem
        {
            itemId = "ITEM_POTION_HP",
            amount = 3,
            displayName = "체력 물약"
        });

        rewards.Add(new RewardItem
        {
            itemId = "ITEM_MATERIAL_COMMON",
            amount = 5,
            displayName = "일반 재료"
        });

        // 빠른 클리어 보너스
        if (request.clearTimeSec < 120)
        {
            rewards.Add(new RewardItem
            {
                itemId = "ITEM_MATERIAL_RARE",
                amount = 1,
                displayName = "희귀 재료"
            });
        }

        return rewards;
    }

    /// <summary>
    /// 통화 보상 계산
    /// </summary>
    private Dictionary<string, int> CalculateCurrencies(EndRunRequest request)
    {
        var currencies = new Dictionary<string, int>();

        if (!request.success)
        {
            currencies["GO"] = 10; // 골드 소량
            return currencies;
        }

        // 기본 골드
        currencies["GO"] = 100;

        // 빠른 클리어 보너스
        if (request.clearTimeSec < 120)
        {
            currencies["GO"] += 50;
        }

        return currencies;
    }

    /// <summary>
    /// 경험치 계산
    /// </summary>
    private int CalculateExp(EndRunRequest request)
    {
        if (!request.success) return 10;

        int baseExp = 100;
        
        // 빠른 클리어 보너스
        if (request.clearTimeSec < 120)
        {
            baseExp = (int)(baseExp * 1.5f);
        }

        return baseExp;
    }

    #endregion
}

#region DTOs

public class EndRunRequest
{
    public string runId { get; set; } = string.Empty;
    public bool success { get; set; }
    public int clearTimeSec { get; set; }
}

public class EndRunResponse
{
    public bool ok { get; set; }
    public string? message { get; set; }
    public List<RewardItem> rewards { get; set; } = new();
    public Dictionary<string, int> currencies { get; set; } = new();
    public int expGained { get; set; }
    public bool isNewRecord { get; set; }
    public int rank { get; set; }
}

public class RewardItem
{
    public string itemId { get; set; } = string.Empty;
    public int amount { get; set; }
    public string? displayName { get; set; }
}

#endregion
