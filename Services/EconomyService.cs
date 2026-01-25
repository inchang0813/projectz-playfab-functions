using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PlayFab;
using AfterHuman.Games.Function.DTOs;

namespace AfterHuman.Games.Function.Services;

/// <summary>
/// PlayFab Economy V2 서비스
/// - 인벤토리 아이템 지급
/// </summary>
public class EconomyService
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;

    public EconomyService(ILogger logger)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5) // PlayFab API 타임아웃 설정
        };
    }

    /// <summary>
    /// PlayFab Economy V2로 보상 지급
    /// </summary>
    /// <param name="playFabId">PlayFab 유저 ID (TitlePlayerAccountId)</param>
    /// <param name="entityToken">Entity Token</param>
    /// <param name="rewards">지급할 보상 목록</param>
    /// <returns>성공 여부</returns>
    public async Task<bool> GrantRewardsAsync(string playFabId, string entityToken, List<RewardItem> rewards)
    {
        if (string.IsNullOrEmpty(playFabId) || string.IsNullOrEmpty(entityToken))
        {
            _logger.LogError("❌ PlayFabId 또는 EntityToken이 누락되었습니다.");
            return false;
        }

        if (rewards == null || rewards.Count == 0)
        {
            _logger.LogInformation("⚠️ 지급할 보상이 없습니다.");
            return true;
        }

        _logger.LogInformation($"🎁 보상 지급 시작: PlayFabId={playFabId}, 보상개수={rewards.Count}");
        
        try
        {
            var titleId = PlayFabSettings.staticSettings.TitleId;
            if (string.IsNullOrEmpty(titleId))
            {
                _logger.LogError("❌ PlayFab TitleId가 설정되지 않았습니다.");
                return false;
            }

            _logger.LogInformation($"✅ PlayFab 설정 확인: TitleId={titleId}");
            
            // EntityToken 헤더 설정
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-EntityToken", entityToken);
            
            foreach (var reward in rewards)
            {
                _logger.LogInformation($"📦 처리 중: {reward.itemId} x{reward.amount}");
                
                // Economy V2 AddInventoryItems API 호출
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
                            Value = reward.itemId
                        }
                    },
                    Amount = reward.amount
                };
                
                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );
                
                var url = $"https://{titleId}.playfabapi.com/Inventory/AddInventoryItems";
                
                _logger.LogInformation($"🌐 API 호출: {url}");
                _logger.LogInformation($"📤 요청: ItemId={reward.itemId}, Amount={reward.amount}");
                
                var response = await _httpClient.PostAsync(url, jsonContent);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"❌ 아이템 지급 실패: Status={response.StatusCode}");
                    _logger.LogError($"❌ 응답: {responseContent}");
                    return false;
                }
                
                _logger.LogInformation($"✅ 아이템 지급 성공: {reward.itemId} x{reward.amount}");
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

    /// <summary>
    /// 리소스 해제
    /// </summary>
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
