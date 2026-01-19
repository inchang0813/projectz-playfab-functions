using System;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using AfterHuman.Games.Function.Models;
using PlayFab;

namespace AfterHuman.Games.Function.Services;

/// <summary>
/// PlayFab 공통 기능 헬퍼
/// - 설정 초기화
/// - 요청 파싱
/// </summary>
public static class PlayFabHelper
{
    /// <summary>
    /// PlayFab 설정 초기화 (환경 변수에서 읽기)
    /// </summary>
    public static void InitializeSettings(ILogger logger)
    {
        // TitleId 설정
        var titleId = Environment.GetEnvironmentVariable("PLAYFAB_TITLE_ID");
        if (!string.IsNullOrEmpty(titleId))
        {
            PlayFabSettings.staticSettings.TitleId = titleId;
            logger.LogInformation($"🔧 PlayFab TitleId 설정: {titleId}");
        }
        else
        {
            logger.LogWarning("⚠️ PLAYFAB_TITLE_ID 환경 변수가 설정되지 않았습니다!");
        }
        
        // SecretKey 설정 (PLAYFAB_SECRET_KEY 또는 PLAYFAB_DEV_SECRET_KEY 모두 지원)
        var secretKey = Environment.GetEnvironmentVariable("PLAYFAB_SECRET_KEY") 
                        ?? Environment.GetEnvironmentVariable("PLAYFAB_DEV_SECRET_KEY");
        if (!string.IsNullOrEmpty(secretKey))
        {
            PlayFabSettings.staticSettings.DeveloperSecretKey = secretKey;
            logger.LogInformation($"🔧 PlayFab SecretKey 설정 완료 (길이: {secretKey.Length})");
        }
        else
        {
            logger.LogWarning("⚠️ PLAYFAB_SECRET_KEY 환경 변수가 설정되지 않았습니다!");
        }
    }

    /// <summary>
    /// PlayFab CloudScript 요청 파싱 (제네릭 버전)
    /// </summary>
    /// <typeparam name="T">파싱할 DTO 타입</typeparam>
    /// <param name="requestBody">HTTP 요청 본문</param>
    /// <param name="logger">로거</param>
    /// <returns>(파싱된 요청, PlayFabId, EntityToken)</returns>
    public static (T? request, string? playFabId, string? entityToken) ParseCloudScriptRequest<T>(
        string requestBody, 
        ILogger logger) where T : class
    {
        T? request = null;
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
                request = JsonSerializer.Deserialize<T>(argJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                // TitlePlayerAccountId 추출
                playFabId = playFabRequest.CallerEntityProfile?.Lineage?.TitlePlayerAccountId;
                
                // EntityToken 추출 (Economy V2 API용)
                entityToken = playFabRequest.TitleAuthenticationContext?.EntityToken;
                
                if (string.IsNullOrEmpty(playFabId))
                {
                    logger.LogWarning("⚠️ TitlePlayerAccountId를 찾을 수 없습니다.");
                }
                else if (string.IsNullOrEmpty(entityToken))
                {
                    logger.LogWarning("⚠️ EntityToken을 찾을 수 없습니다.");
                }
                else
                {
                    logger.LogInformation($"☁️ PlayFab CloudScript 방식으로 파싱 성공 (Entity: {playFabId})");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug($"PlayFab wrapper 파싱 실패 (로컬 테스트 시도): {ex.Message}");
        }

        // 로컬 테스트 방식 (직접 DTO)
        if (request == null)
        {
            try
            {
                request = JsonSerializer.Deserialize<T>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                logger.LogInformation("🔧 로컬 테스트 방식으로 파싱 성공");
            }
            catch (Exception ex)
            {
                logger.LogError($"❌ 요청 파싱 실패: {ex.Message}");
            }
        }

        return (request, playFabId, entityToken);
    }
}
