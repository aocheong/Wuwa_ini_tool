# WuwaIniToolCs (v1.1)

WinForms 기반 C# 패치 툴입니다. 프리셋 `Engine.ini` 적용, 캐시 정리, 순정 복원, 게임 실행 보조 기능을 제공합니다.

## 주요 기능
- 경로 지정: `Win64` 자동 탐색/수동 선택 + `WindowsNoEditor` 커스텀 경로 탐색
- 프리셋 적용: RT ON/OFF 기반 버튼 구성, 적용 전 확인, 백업 생성, 실패 시 재시도
- 캐시 제거: `PSO`/`PSOReport` + 셰이더 캐시 정리, 삭제 후 재캐싱 안내 팝업
- 순정 복원: 현재 대상 `Engine.ini` 삭제(필요 시 UAC 승격)
- 게임 실행: 실행 전 `Engine.ini` 정리 + 실행 인자 전달
- 커스텀 경로 정책: 커스텀 경로에서는 게임 실행 버튼이 차단되고 안내 팝업 표시
- 팝업/트레이 UX: 커스텀 팝업 사용, 실행 안내 자동 닫힘(2초), 트레이 최소화 연동

## 경로 정책
- 기본 대상: `...\Client\Binaries\Win64`
- 커스텀 대상: `...\Client\Saved\Config\WindowsNoEditor`
- 내부적으로 대상 경로를 공통 처리하며 `TargetFolderToEngineIni(...)`로 `Engine.ini`를 계산합니다.

### 커스텀 경로 실행 제한
커스텀 경로가 지정된 상태에서 `게임 실행`을 누르면 실행하지 않고 아래 안내를 표시합니다.

`해당 경로는 구글플레이/모드 유저가 사용하는 경로입니다.`
`구글플레이/모드 유저는 우회 실행을 사용할 수 없습니다.`

## 캐시 파일 (`app_cache.json`)
- `game_exe_path`: 마지막으로 확인된 게임 exe 경로
- `last_target_dir`: 마지막으로 선택한 대상 경로(Win64/커스텀 공통)
- `rt_enabled`: RT 체크 상태

앱 시작 시에는 `last_target_dir`를 우선 복원합니다. 즉, Win64와 커스텀 경로 중 마지막으로 저장된 경로가 다음 실행에 이어집니다.

## 관리자(UAC) 분기
- 보호 경로에서 쓰기 작업이 필요할 때만 관리자 재실행
- 관리자 인자:
  - `--elevated-copy`
  - `--elevated-launch`
  - `--elevated-clear-cache`
  - `--elevated-delete-ini`

## 프로젝트 구성
- `Program.cs`: 진입점
- `MainForm.cs`: 메인 UI/이벤트/실행 흐름
- `ElevatedMode.cs`: 관리자 인자 처리
- `ElevationService.cs`: 권한 확인/재실행
- `GamePathService.cs`: Win64/커스텀 경로 탐색
- `EngineIniService.cs`: INI 정리, 백업, 캐시 삭제
- `CacheService.cs`: `app_cache.json` 로드/저장
- `PopupService.cs`: 공통 팝업 UI
- `AppPaths.cs`, `AppConstants.cs`: 경로/상수
- `AppLogger.cs`: 상세 로그

## 로컬 빌드
```powershell
cd WuwaIniToolCs
dotnet restore
dotnet build -c Release
```

## 퍼블리시 (번들 배포, 런타임 미포함)
```powershell
dotnet publish "WuwaIniToolCs\WuwaIniToolCs.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "dist\wuwa_patch_tool_v1.1"
```

## SHA256 해시 동봉 예시
```powershell
$exe = "dist\wuwa_patch_tool_v1.1\WuwaIniToolCs.exe"
$hash = (Get-FileHash -Algorithm SHA256 $exe).Hash.ToLower()
"$hash *WuwaIniToolCs.exe" | Set-Content "dist\wuwa_patch_tool_v1.1\SHA256SUMS.txt" -Encoding ascii
```

## 참고
- 실행 파일은 `presets`/`assets`를 exe 옆 경로 기준으로 읽습니다.
- 권한/파일 점유 이슈 발생 시 `permission_debug_log.txt`를 함께 확인하면 원인 파악이 빠릅니다.
