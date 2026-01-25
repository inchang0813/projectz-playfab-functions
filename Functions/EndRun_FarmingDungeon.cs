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
using AfterHuman.Games.Function.Services;
using PlayFab;
using PlayFab.ServerModels;
using PlayFab.AuthenticationModels;

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
        
        // PlayFab 설정 초기화
        PlayFabHelper.InitializeSettings(_logger);
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

            // PlayFab CloudScript 요청 파싱 (공통 헬퍼 사용)
            var (request, playFabId, entityToken) = PlayFabHelper.ParseCloudScriptRequest<EndRunFarmingDungeonRequest>(
                requestBody, 
                _logger
            );

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

            // ⚠️ [서비스 전 필수] Redis/Database에서 런 상태 검증
            // 현재는 클라이언트 시간만 검증하지만, 서비스 전 반드시 추가 필요:
            // 1. runId 존재 여부 확인 (StartRun에서 생성된 런인가?)
            // 2. 중복 완료 방지 (이미 보상 지급된 런인가?)
            // 3. 서버 시간 기준 검증 (StartRun 시각 vs EndRun 시각 차이가 합리적인가?)
            // var runState = await GetRunStateAsync(request.runId);
            // if (runState == null) return NotFound("Run not found");
            // if (runState.IsCompleted) return BadRequest("Run already completed");
            // var serverElapsed = (DateTimeOffset.UtcNow - runState.StartTime).TotalSeconds;
            // if (Math.Abs(request.clearTimeSec - serverElapsed) > 5) return BadRequest("Time manipulation detected");
            
            // ⚠️ 보안: 클라이언트 값 검증 (임시, 서버 시간 검증으로 대체 예정)
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

            // PlayFab API로 실제 보상 지급
            if (!string.IsNullOrEmpty(playFabId) && !string.IsNullOrEmpty(entityToken) && rewards.Count > 0)
            {
                var economyService = new EconomyService(_logger);
                var grantResult = await economyService.GrantRewardsAsync(playFabId, entityToken, rewards);
                economyService.Dispose();
                
                if (!grantResult)
                {
                    _logger.LogWarning("⚠️ 보상 지급 실패 (PlayFab API 오류)");
                    return new ObjectResult(new EndRunFarmingDungeonResponse
                    {
                        ok = false,
                        message = "Failed to grant rewards"
                    })
                    {
                        StatusCode = 500
                    };
                }
            }
            else if (string.IsNullOrEmpty(playFabId))
            {
                _logger.LogWarning("⚠️ PlayFabId 없음 - 로컬 테스트 모드로 간주");
            }

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
        const int RUN_DURATION_SEC = 300; // 30초 테스트용
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
    private List<DTOs.RewardItem> CalculateRewards(EndRunFarmingDungeonRequest request)
    {
        var rewards = new List<RewardItem>();

        if (!request.success)
        {
            // 생존 실패 시 보상 없음
            _logger.LogInformation("⚠️ 생존 실패로 인한 보상 없음");
            return rewards;
        }

        // 클라이언트가 파밍한 아이템 추가
        if (request.lootedItems != null && request.lootedItems.Count > 0)
        {
            _logger.LogInformation($"📦 파밍 아이템 {request.lootedItems.Count}개 처리 중");
            foreach (var lootedItem in request.lootedItems)
            {
                if (string.IsNullOrEmpty(lootedItem.itemId) || lootedItem.amount <= 0)
                {
                    _logger.LogWarning($"⚠️ 잘못된 아이템 데이터: ItemId={lootedItem.itemId}, Amount={lootedItem.amount}");
                    continue;
                }

                rewards.Add(new RewardItem
                {
                    itemId = lootedItem.itemId,
                    amount = lootedItem.amount,
                    displayName = lootedItem.itemId // 실제로는 아이템 마스터 데이터에서 가져와야 함
                });
                
                _logger.LogInformation($"✅ 파밍 아이템 추가: {lootedItem.itemId} x{lootedItem.amount} (Container: {lootedItem.containerId})");
            }
        }

        // 생존 성공 시 기본 재화 지급
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
