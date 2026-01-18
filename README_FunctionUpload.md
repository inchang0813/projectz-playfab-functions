Function App : 
ah-projectz-functions
RG(Resource Group) :
 AfterHuman-Dev

## Azure 로그인 (최초 1회만)

```bash
az login --tenant d29bacce-cd25-4cba-811e-8a2bd1848413 --use-device-code
```
※ 로그인 후 브라우저에서 인증 완료하면 이후 배포 시 재로그인 불필요

## 배포 순서 (Deploy Steps)

1) 로컬 프로젝트 폴더로 이동 (host.json 있는 곳) - 현재 경로면 생략
cd D:\PROJECTS\ProjectZ\PlayfabFunctions

2) (권장) 빌드 캐시 정리 (배포 용량 최소화)
dotnet clean

3) 업로드(배포) 실행 (추천: 원격 빌드)
func azure functionapp publish ah-projectz-functions --build remote
   ※ 배포 시 자동으로 Function App이 재시작됩니다
   ※ .funcignore 파일로 불필요한 파일 자동 제외

4) 배포 성공 확인
func azure functionapp list-functions ah-projectz-functions

5) (선택) 배포 후 문제 발생 시에만 수동 재시작
az functionapp restart -g AfterHuman-Dev -n ah-projectz-functions