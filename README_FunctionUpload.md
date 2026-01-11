Function App : 
ah-projectz-functions
RG(Resource Group) :
 AfterHuman-Dev

// 로그인
az login --tenant d29bacce-cd25-4cba-811e-8a2bd1848413 --use-device-code

az storage account show-connection-string -g AfterHuman-Dev -n ahprojectzstdev01 -o tsv
az functionapp config appsettings set -g AfterHuman-Dev -n ah-projectz-functions --settings "AzureWebJobsStorage=ahprojectzstdev01"

## 배포 순서 (Deploy Steps)

1) 로컬 프로젝트 폴더로 이동 (host.json 있는 곳) - 현재 경로면 생략
cd D:\PROJECTS\ProjectZ\PlayfabFunctions

2) 업로드(배포) 실행 (추천: 원격 빌드)
func azure functionapp publish ah-projectz-functions --build remote
   ※ 배포 시 자동으로 Function App이 재시작됩니다

3) 배포 성공 확인
func azure functionapp list-functions ah-projectz-functions

4) (선택) 배포 후 문제 발생 시에만 수동 재시작
az functionapp restart -g AfterHuman-Dev -n ah-projectz-functions