Function App : 
ah-projectz-functions
RG(Resource Group) :
 AfterHuman-Dev

az storage account show-connection-string -g AfterHuman-Dev -n ahprojectzstdev01 -o tsv
az functionapp config appsettings set -g AfterHuman-Dev -n ah-projectz-functions --settings "AzureWebJobsStorage=ahprojectzstdev01"

1) Function App 재시작
az functionapp restart -g AfterHuman-Dev -n ah-projectz-functions

2) 로컬 프로젝트 폴더로 이동 (host.json 있는 곳) - 현재 경로면 생략
cd D:\PROJECTS\ProjectZ\PlayfabFunctions

3) 업로드(배포) 실행 (추천: 원격 빌드)
func azure functionapp publish ah-projectz-functions --build remote

4) 배포 성공 확인
func azure functionapp list-functions ah-projectz-functions