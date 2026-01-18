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
        
        // PlayFab 설정 초기화 (환경 변수에서 읽기)
        var titleId = Environment.GetEnvironmentVariable("PLAYFAB_TITLE_ID");
        if (!string.IsNullOrEmpty(titleId))
        {
            PlayFabSettings.staticSettings.TitleId = titleId;
            _logger.LogInformation($"🔧 PlayFab TitleId 설정: {titleId}");
        }
        else
        {
            _logger.LogWarning("⚠️ PLAYFAB_TITLE_ID 환경 변수가 설정되지 않았습니다!");
        }
        
        // PLAYFAB_SECRET_KEY 또는 PLAYFAB_DEV_SECRET_KEY 모두 지원
        var secretKey = Environment.GetEnvironmentVariable("PLAYFAB_SECRET_KEY") 
                        ?? Environment.GetEnvironmentVariable("PLAYFAB_DEV_SECRET_KEY");
        if (!string.IsNullOrEmpty(secretKey))
        {
            PlayFabSettings.staticSettings.DeveloperSecretKey = secretKey;
            _logger.LogInformation($"🔧 PlayFab SecretKey 설정 완료 (길이: {secretKey.Length})");
        }
        else
        {
            _logger.LogWarning("⚠️ PLAYFAB_SECRET_KEY 환경 변수가 설정되지 않았습니다!");
        }
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
            string? entityToken = null;

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
                    // CloudScript가 전달하는 TitlePlayerAccountId 사용 (Entity.Id와 동일)
                    playFabId = playFabRequest.CallerEntityProfile?.Lineage?.TitlePlayerAccountId;
                    // EntityToken 추출 (Economy V2 API용)
                    entityToken = playFabRequest.TitleAuthenticationContext?.EntityToken;
                    
                    if (string.IsNullOrEmpty(playFabId))
                    {
                        _logger.LogError("❌ TitlePlayerAccountId를 찾을 수 없습니다. Economy V2 호출 불가.");
                    }
                    else if (string.IsNullOrEmpty(entityToken))
                    {
                        _logger.LogError("❌ EntityToken을 찾을 수 없습니다. Economy V2 호출 불가.");
                    }
                    else
                    {
                        _logger.LogInformation($"☁️ PlayFab CloudScript 방식으로 파싱 성공 (Entity: {playFabId})");
                    }
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

            // PlayFab API로 실제 보상 지급
            if (!string.IsNullOrEmpty(playFabId) && !string.IsNullOrEmpty(entityToken) && rewards.Count > 0)
            {
                var grantResult = await GrantRewardsAsync(playFabId, entityToken, rewards);
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

    #region PlayFab API 호출

    /// <summary>
    /// PlayFab에 실제 보상 지급 (Economy V2 방식)
    /// ⚠️ Economy V2에서는 REST API를 직접 호출해야 함 (Server SDK 제한)
    /// </summary>
    private async Task<bool> GrantRewardsAsync(string playFabId, string entityToken, List<RewardItem> rewards)
    {
        _logger.LogInformation($"🎁 보상 지급 시작: PlayFabId={playFabId}, 보상개수={rewards.Count}");
        
        try
        {
            var titleId = PlayFabSettings.staticSettings.TitleId;
            _logger.LogInformation($"✅ PlayFab 설정 확인: TitleId={titleId}");
            
            // Economy V2 REST API 호출 (EntityToken 사용)
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.DefaultRequestHeaders.Add("X-EntityToken", entityToken);
            
            foreach (var reward in rewards)
            {
                _logger.LogInformation($"📦 처리 중: {reward.friendlyId} x{reward.amount}");
                
                // Economy V2 AddInventoryItems API 호출 (Friendly ID는 AlternateId로 전달)
                // ⚠️ TitlePlayerAccountId 사용 시 title_player_account 타입 사용
                var requestBody = new
                {
                    Entity = new
                    {
                        Id = playFabId,
                        Type = "title_player_account"
                    },
                    Item = new
                    {
                        AlternateId = new
                        {
                            Type = "FriendlyId",
                            Value = reward.friendlyId
                        }
                    },
                    Amount = reward.amount
                };
                
                var jsonContent = new System.Net.Http.StringContent(
                    JsonSerializer.Serialize(requestBody),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );
                
                var url = $"https://{titleId}.playfabapi.com/Inventory/AddInventoryItems";
                
                _logger.LogInformation($"🌐 API 호출: {url}");
                _logger.LogInformation($"📤 요청: ItemId={reward.friendlyId}, Amount={reward.amount}");
                
                var response = await httpClient.PostAsync(url, jsonContent);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"❌ 아이템 지급 실패: Status={response.StatusCode}");
                    _logger.LogError($"❌ 응답: {responseContent}");
                    return false;
                }
                
                _logger.LogInformation($"✅ 아이템 지급 성공: {reward.friendlyId} x{reward.amount}");
                _logger.LogInformation($"📥 응답: {responseContent}");
            }

            _logger.LogInformation($"✅ 모든 보상 지급 완료");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ GrantRewardsAsync 예외: {ex.Message}");
            _logger.LogError($"❌ StackTrace: {ex.StackTrace}");
            return false;
        }
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
            friendlyId = "currency_z_coin",
            amount = 100,
            displayName = "파밍 재화"
        });

        // ⚠️ 실제로는 던전 데이터, 난이도, 클리어 시간 등을 고려해야 함
        // TODO: 던전 보상 테이블 참조

        return rewards;
    }

    #endregion
}
