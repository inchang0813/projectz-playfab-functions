using System;
using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using AfterHuman.Games.Function.DTOs;
using AfterHuman.Games.Function.Models;
using AfterHuman.Games.Function.Services;

namespace AfterHuman.Games.Function;

/// <summary>
/// Farming Dungeon 런 시작 Function
/// - 고유 runId 발급
/// - 맵 생성용 seed 발급
/// - 서버 시간 반환
/// </summary>
public class StartRun_FarmingDungeon
{
    private readonly ILogger<StartRun_FarmingDungeon> _logger;

    public StartRun_FarmingDungeon(ILogger<StartRun_FarmingDungeon> logger)
    {
        _logger = logger;
    }

    [Function("StartRun_FarmingDungeon")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
    {
        _logger.LogInformation("🏃 StartRun_FarmingDungeon 호출");

        try
        {
            // 요청 파싱 (공통 헬퍼 사용)
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation($"📥 요청 본문: {requestBody}");

            var (request, playFabId, _) = PlayFabHelper.ParseCloudScriptRequest<StartRunFarmingDungeonRequest>(
                requestBody, 
                _logger
            );

            if (request == null)
            {
                _logger.LogWarning("⚠️ 요청 파싱 실패");
                return new BadRequestObjectResult(new StartRunFarmingDungeonResponse
                {
                    ok = false,
                    message = "Invalid request format"
                });
            }

            if (!string.IsNullOrEmpty(playFabId))
            {
                _logger.LogInformation($"👤 PlayFabId: {playFabId}");
            }

            // 던전 ID 검증 (개발 단계: 생략 가능)
            string dungeonId = request.dungeonId ?? "FD_TEST_001";
            _logger.LogInformation($"📍 DungeonId: {dungeonId}");

            // runId 생성 (고유 식별자)
            string runId = GenerateRunId();
            
            // seed 생성 (맵 생성용)
            int seed = GenerateSeed();
            
            // 서버 시간 (Unix timestamp)
            long serverTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // TODO: Redis/Database에 런 상태 저장
            // await SaveRunStateAsync(runId, dungeonId, serverTime);

            var response = new StartRunFarmingDungeonResponse
            {
                ok = true,
                runId = runId,
                seed = seed,
                serverTime = serverTime,
                dungeonId = dungeonId,
                runDurationSec = 300 // 300초 테스트용
            };

            _logger.LogInformation($"✅ 런 시작 성공: RunId={runId}, Seed={seed}");
            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ StartRun_FarmingDungeon 실패: {ex.Message}");
            return new ObjectResult(new StartRunFarmingDungeonResponse
            {
                ok = false,
                message = $"Internal server error: {ex.Message}"
            })
            {
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// 고유 RunId 생성
    /// </summary>
    private string GenerateRunId()
    {
        // 타임스탬프 + GUID로 고유성 보장
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string guid = Guid.NewGuid().ToString("N").Substring(0, 8);
        return $"RUN_{timestamp}_{guid}";
    }

    /// <summary>
    /// 맵 생성용 시드 생성
    /// </summary>
    private int GenerateSeed()
    {
        // Random seed 생성 (양수)
        return Math.Abs(Guid.NewGuid().GetHashCode());
    }
}