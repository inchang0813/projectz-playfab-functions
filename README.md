# ProjectZ PlayFab Azure Functions

ProjectZ에서 PlayFab(또는 클라이언트)에서 호출하는 Azure Functions(.NET) 모음을 관리하는 프로젝트입니다.

- 프로젝트 루트(로컬): `D:\PROJECTS\ProjectZ\PlayfabFunctions`
- 주요 함수 네이밍: `StartRun_<Mode>`, `EndRun_<Mode>`
  - 예: `StartRun_FarmingDungeon`, `EndRun_FarmingDungeon`

---

## 1) 사전 준비(필수)

### 설치 확인
- .NET SDK (프로젝트 템플릿에 맞는 버전)
- Azure Functions Core Tools (func CLI)

터미널에서 아래 명령이 동작하면 준비 완료:

```bash
func --version
dotnet --version
```

---

## 2) 프로젝트 구조(권장)

함수 파일은 루트에 둬도 동작하지만, 관리 편의를 위해 `Functions/` 폴더로 정리합니다.

예시:

```
PlayfabFunctions/
  Functions/
    StartRun_FarmingDungeon.cs
    EndRun_FarmingDungeon.cs
  host.json
  local.settings.json
  *.csproj
  (Program.cs 등 템플릿에 따라 존재)
```

> 참고: Azure Functions는 파일이 어느 폴더에 있든 **프로젝트에 포함되어 빌드되면** 함수로 인식됩니다.

---

## 3) 새 Function 추가 방법 (func CLI)

이미 `Create New Project`로 프로젝트를 만든 상태라면,
새 함수는 **새 프로젝트를 만드는 것이 아니라** 같은 프로젝트 폴더에서 추가합니다.

### 3.1 프로젝트 루트로 이동

```bash
cd D:\PROJECTS\ProjectZ\PlayfabFunctions
```

### 3.2 HTTP Trigger 함수 추가

```bash
func new --name EndRun_FarmingDungeon --template "HTTP trigger" --authlevel "function"
```

- `--name`: 생성될 함수 이름 (파일/클래스 생성 기준)
- `--template`: 템플릿 종류 (PlayFab/클라 호출이면 보통 HTTP trigger)

추가로 모드가 늘어나면 같은 규칙으로 생성:

```bash
func new --name StartRun_BossRush --template "HTTP trigger" --authlevel "function"
func new --name EndRun_BossRush --template "HTTP trigger" --authlevel "function"
```

---

## 4) 파일 정리(Functions 폴더로 이동)

`func new`로 생성된 `.cs` 파일을 `Functions/` 폴더로 이동해도 문제 없습니다.

예:

- `EndRun_FarmingDungeon.cs` → `Functions/EndRun_FarmingDungeon.cs`

> 이동 후 로컬 실행 시 함수 목록에 표시되면 정상입니다.

---

## 5) 로컬 실행 및 함수 등록 확인

프로젝트 루트에서 실행:

```bash
func start
```

실행 로그에 아래처럼 Functions 목록이 뜨면 등록 완료:

- `StartRun_FarmingDungeon`
- `EndRun_FarmingDungeon`

---

## 6) 로컬 테스트 (HTTP 호출)

함수 URL은 로컬 실행 로그에 표시되는 엔드포인트를 사용합니다.
(예: `http://localhost:7071/api/EndRun_FarmingDungeon`)

### 예시(curl)
```bash
curl -X POST "http://localhost:7071/api/EndRun_FarmingDungeon" ^
  -H "Content-Type: application/json" ^
  -d "{\"runId\":\"test\",\"result\":\"success\"}"
```

> Authorization Level이 `Function`인 경우, `?code=<FUNCTION_KEY>`가 필요할 수 있습니다.

---

## 7) 네이밍 규칙(권장)

### Function Name
- 입장(시작): `StartRun_<Mode>`
- 종료(정산/확정): `EndRun_<Mode>`

### Mode 이름
- PascalCase 권장 (예: `FarmingDungeon`, `BossRush`, `Arena`)
- 약어 남발 지양 (`FD`, `BR` 등은 장기적으로 혼란)

---

## 8) 운영 팁(권장)

- 공통 처리(인증/검증/runId 발급/로그 포맷/응답 포맷)는
  `Core/` 같은 공통 폴더(또는 별도 Class Library)로 분리하면 중복을 크게 줄일 수 있습니다.
- 모드별 로직이 커지면 `Modes/<Mode>/`로 분리해서 유지보수성을 확보합니다.

---

## Troubleshooting

### Q. 함수가 로컬 실행 목록에 안 떠요
1) `func start` 실행 위치가 프로젝트 루트인지 확인 (`host.json` 있는 폴더)
2) 파일 이동 후 빌드가 되는지 확인 (`dotnet build`)
3) `.csproj`가 `Compile Remove` 등으로 특정 폴더/파일을 제외하고 있는지 확인(드문 케이스)
