using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using AfterHuman.Games.Function.DTOs;
using AfterHuman.Games.Function.Models;

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
            _logger.LogInformation($"📥 요청 본문: {requestBody}");

            EndRunFarmingDungeonRequest? request = null;
            string? playFabId = null;

            // PlayFab CloudScript 방식 (FunctionArgument wrapper)
            try
            {
                var playFabRequest = JsonSerializer.Deserialize<PlayFabFunctionRequest>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (playFabRequest?.FunctionArgument is JsonElement argElement)
                {
                    var argJson = argElement.GetRawText();
                    request = JsonSerializer.Deserialize<EndRunFarmingDungeonRequest>(argJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    playFabId = playFabRequest.CallerEntityProfile?.Lineage?.MasterPlayerAccountId;
                    _logger.LogInformation("☁️ PlayFab CloudScript 방식으로 파싱 성공");
                }
            }
            catch
            {
                // PlayFab wrapper 파싱 실패 시 직접 파싱 시도 (로컬 테스트용)
            }

            // 로컬 테스트 방식 (직접 DTO)
            if (request == null)
            {
                request = JsonSerializer.Deserialize<EndRunFarmingDungeonRequest>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                _logger.LogInformation("🔧 로컬 테스트 방식으로 파싱 성공");
            }

            if (request == null || string.IsNullOrEmpty(request.runId))
            {
                _logger.LogWarning("⚠️ 요청 파싱 실패 또는 runId 누락");
                return new BadRequestObjectResult(new EndRunFarmingDungeonResponse
                {
                    ok = false,
                    message = "Invalid request: runId is required"
                });
            }

            _logger.LogInformation($"📍 RunId: {request.runId}, Success: {request.success}, Time: {request.clearTimeSec}s");
            if (!string.IsNullOrEmpty(playFabId))
            {
                _logger.LogInformation($"👤 PlayFabId: {playFabId}");
            }

            // TODO: Redis/Database에서 런 상태 검증
            // var runState = await GetRunStateAsync(request.runId);
            // if (runState == null) return NotFound("Run not found");
            // if (runState.IsCompleted) return BadRequest("Run already completed");
            
            // ⚠️ 보안: 클라이언트 값 검증
            if (!ValidateRunData(request))
            {
                _logger.LogWarning($"⚠️ 런 검증 실패: {request.runId}");
                return new BadRequestObjectResult(new EndRunFarmingDungeonResponse
                {
                    ok = false,
                    message = "Run validation failed"
                });
            }

            // 보상 계산 (서버 로직)
            var rewards = CalculateRewards(request);

            // TODO: PlayFab API로 실제 보상 지급
            // if (!string.IsNullOrEmpty(playFabId) && rewards.Count > 0)
            // {
            //     await GrantRewardsAsync(playFabId, rewards);
            // }

            var response = new EndRunFarmingDungeonResponse
            {
                ok = true,
                message = request.success ? "Dungeon cleared!" : "Dungeon failed",
                rewards = rewards
            };

            _logger.LogInformation($"✅ 런 종료 성공: {rewards.Count}개 보상");
            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ EndRun_FarmingDungeon 실패: {ex.Message}");
            return new ObjectResult(new EndRunFarmingDungeonResponse
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
    private bool ValidateRunData(EndRunFarmingDungeonRequest request)
    {
        const int RUN_DURATION_SEC = 30; // 30초 테스트용
        const int TIME_BUFFER_SEC = 10;   // 네트워크 지연 등을 고려한 버퍼
        
        // success=true (생존 성공): 진행 시간 근처에서만 허용
        if (request.success)
        {
            int minExpectedTime = RUN_DURATION_SEC - TIME_BUFFER_SEC; // 20초
            if (request.clearTimeSec < minExpectedTime)
            {
                _logger.LogWarning($"⚠️ 생존 시간 미달: {request.clearTimeSec}s (최소 {minExpectedTime}s)");
                return false;
            }
        }

        // 최대 시간 검증 (success 관계없이 공통)
        int maxAllowedTime = RUN_DURATION_SEC + TIME_BUFFER_SEC; // 40초
        if (request.clearTimeSec > maxAllowedTime)
        {
            _logger.LogWarning($"⚠️ 최대 시간 초과: {request.clearTimeSec}s (최대 {maxAllowedTime}s)");
            return false;
        }

        // 최소 시간 검증 (비정상적으로 짧은 시간 방지)
        if (request.clearTimeSec < 1)
        {
            _logger.LogWarning($"⚠️ 비정상적인 플레이 시간: {request.clearTimeSec}s");
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
    /// 보상 계산 (아이템 + 통화 통합)
    /// </summary>
    private List<RewardItem> CalculateRewards(EndRunFarmingDungeonRequest request)
    {
        var rewards = new List<RewardItem>();

        if (!request.success)
        {
            // 생존 실패 시 보상 없음
            _logger.LogInformation("⚠️ 생존 실패로 인한 보상 없음");
            return rewards;
        }

        // 생존 성공 시 재화 지급
        rewards.Add(new RewardItem
        {
            itemId = "currency_z_coin",
            amount = 100,
            displayName = "파밍 재화"
        });

        // ⚠️ 실제로는 던전 데이터, 난이도, 클리어 시간 등을 고려해야 함
        // TODO: 던전 보상 테이블 참조

        return rewards;
    }

    #endregion
}
