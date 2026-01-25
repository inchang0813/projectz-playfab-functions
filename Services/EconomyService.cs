using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PlayFab;
using AfterHuman.Games.Function.DTOs;

namespace AfterHuman.Games.Function.Services;

public class EconomyService : IDisposable
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;

    public EconomyService(ILogger logger)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    /// <summary>
    /// 보상을 "가능한 한" 한 번의 호출로 지급 (ExecuteInventoryOperations 사용)
    /// - 동일 itemId는 amount 합산
    /// - 최대 50 ops 제한 때문에, 50종류 초과면 chunk로 나눠서 최소 횟수로 호출
    /// </summary>
    public async Task<bool> GrantRewardsAsync(string playFabId, string entityToken, List<RewardItem> rewards, string? idempotencyId = null)
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

        var titleId = PlayFabSettings.staticSettings.TitleId;
        if (string.IsNullOrEmpty(titleId))
        {
            _logger.LogError("❌ PlayFab TitleId가 설정되지 않았습니다.");
            return false;
        }

        // ✅ 같은 itemId는 합산해서 op 수를 줄임
        var merged = rewards
            .Where(r => !string.IsNullOrWhiteSpace(r.itemId) && r.amount > 0)
            .GroupBy(r => r.itemId)
            .Select(g => new RewardItem { itemId = g.Key, amount = g.Sum(x => x.amount) })
            .ToList();

        if (merged.Count == 0)
        {
            _logger.LogInformation("⚠️ 유효한 보상이 없습니다.");
            return true;
        }

        _logger.LogInformation($"🎁 보상 지급 시작(배치): PlayFabId={playFabId}, 항목종류={merged.Count}");

        // ✅ 헤더는 요청마다 넣는 게 안전하지만, 단일 호출 기준이면 아래처럼 한 번 세팅도 OK
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("X-EntityToken", entityToken);

        // ExecuteInventoryOperations: 최대 50 operations 제한
        const int maxOpsPerCall = 50;

        // idempotencyId: 재시도/중복 호출 방지용(가능하면 런 세션ID 같은 걸 넣는 걸 추천)
        // 같은 결과 재호출 가능성이 있으면 반드시 외부에서 고정 값으로 넣어주세요.
        // (ex. dungeonRunId를 그대로 idempotencyId로)
        var baseIdempotency = string.IsNullOrWhiteSpace(idempotencyId) ? Guid.NewGuid().ToString("N") : idempotencyId;

        for (int i = 0; i < merged.Count; i += maxOpsPerCall)
        {
            var chunk = merged.Skip(i).Take(maxOpsPerCall).ToList();
            var chunkIdempotency = (merged.Count <= maxOpsPerCall)
                ? baseIdempotency
                : $"{baseIdempotency}_{(i / maxOpsPerCall) + 1}";

            var operations = chunk.Select(reward => new
            {
                Add = new
                {
                    Item = new
                    {
                        AlternateId = new
                        {
                            Type = "FriendlyId",
                            Value = reward.itemId
                        }
                    },
                    Amount = reward.amount
                }
            }).ToList();

            var requestBody = new
            {
                Entity = new
                {
                    Id = playFabId,
                    Type = "title_player_account"
                },
                CollectionId = "default",
                IdempotencyId = chunkIdempotency,
                Operations = operations
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"https://{titleId}.playfabapi.com/Inventory/ExecuteInventoryOperations";
            _logger.LogInformation($"📦 배치 지급 호출: ops={operations.Count}, IdempotencyId={chunkIdempotency}");

            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"❌ 배치 지급 실패: Status={response.StatusCode}");
                _logger.LogError($"❌ 응답: {responseContent}");
                return false;
            }

            _logger.LogInformation($"✅ 배치 지급 성공: ops={operations.Count}");
        }

        _logger.LogInformation("✅ 모든 보상 지급 완료");
        return true;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}