# Services - 공통 코드 정리

Azure Functions에서 재사용 가능한 공통 코드를 모아둔 폴더입니다.

## 📁 파일 구조

```
Services/
├── PlayFabHelper.cs      # PlayFab 설정 및 요청 파싱
├── EconomyService.cs     # Economy V2 API 호출
└── README.md            # 이 문서
```

## 🔧 PlayFabHelper.cs

### 기능
- **PlayFab 설정 초기화**: 환경 변수에서 TitleId, SecretKey 로드
- **CloudScript 요청 파싱**: PlayFab wrapper + 로컬 테스트 방식 자동 처리

### 사용 예제

```csharp
using AfterHuman.Games.Function.Services;

// 1. 생성자에서 PlayFab 초기화
public MyFunction(ILogger<MyFunction> logger)
{
    _logger = logger;
    PlayFabHelper.InitializeSettings(_logger);
}

// 2. 요청 파싱 (제네릭)
var (request, playFabId, entityToken) = PlayFabHelper.ParseCloudScriptRequest<MyRequestDto>(
    requestBody, 
    _logger
);
```

### 반환값
- `request`: 파싱된 DTO 객체
- `playFabId`: TitlePlayerAccountId (유저 고유 ID)
- `entityToken`: Economy V2 API 호출용 토큰

## 💰 EconomyService.cs

### 기능
- **Economy V2 인벤토리 아이템 지급**: FriendlyId 기반 아이템 추가

### 사용 예제

```csharp
using AfterHuman.Games.Function.Services;

// 보상 목록 준비
var rewards = new List<RewardItem>
{
    new RewardItem 
    { 
        friendlyId = "currency_z_coin", 
        amount = 100,
        displayName = "파밍 재화"
    }
};

// Economy 서비스로 지급
var economyService = new EconomyService(_logger);
var success = await economyService.GrantRewardsAsync(playFabId, entityToken, rewards);
economyService.Dispose();

if (!success)
{
    // 지급 실패 처리
}
```

### 주의사항
- `playFabId`는 **TitlePlayerAccountId** 여야 함 (MasterPlayerAccountId 아님)
- `entityToken`은 CloudScript 요청에서 추출 필요
- FriendlyId는 PlayFab Economy Catalog에 등록되어 있어야 함

## 📋 적용된 Function 목록

### ✅ 리팩토링 완료
- [x] `EndRun_FarmingDungeon.cs`
  - PlayFab 초기화 → `PlayFabHelper.InitializeSettings()`
  - 요청 파싱 → `PlayFabHelper.ParseCloudScriptRequest()`
  - 보상 지급 → `EconomyService.GrantRewardsAsync()`

- [x] `StartRun_FarmingDungeon.cs`
  - 요청 파싱 → `PlayFabHelper.ParseCloudScriptRequest()`

### 📝 향후 추가 예정
- Redis/Database 연동 서비스
- 검증 로직 공통화 (시간, runId 등)
- 던전 데이터 관리 서비스

## 🚀 새 Function 추가 시 가이드

1. **생성자에서 초기화**
```csharp
public MyFunction(ILogger<MyFunction> logger)
{
    _logger = logger;
    PlayFabHelper.InitializeSettings(_logger); // PlayFab 사용 시
}
```

2. **요청 파싱**
```csharp
var (request, playFabId, entityToken) = PlayFabHelper.ParseCloudScriptRequest<MyDto>(
    requestBody, 
    _logger
);
```

3. **보상 지급 (필요 시)**
```csharp
var economyService = new EconomyService(_logger);
await economyService.GrantRewardsAsync(playFabId, entityToken, rewards);
economyService.Dispose();
```

## 🔒 환경 변수 설정

`local.settings.json`에 필수 환경 변수 설정:

```json
{
  "Values": {
    "PLAYFAB_TITLE_ID": "YOUR_TITLE_ID",
    "PLAYFAB_SECRET_KEY": "YOUR_SECRET_KEY"
  }
}
```

Azure Portal에서도 동일하게 설정 필요합니다.
