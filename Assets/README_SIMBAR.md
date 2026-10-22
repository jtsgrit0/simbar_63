# SimBar — Live Music Pub Simulator

## 언어 설정

- 기본 언어는 한국어입니다. 톱니바퀴 또는 로비·안내·결과 화면의 `설정` 버튼에서 `한국어` / `English`를 선택합니다.
- 선택한 언어는 즉시 적용되며 `SimBar.Language` 설정으로 저장되어 다음 실행에도 유지됩니다. 영업 진행 상황과 별도로 저장합니다.
- 로비, 안내, 주문·음료 따르기, 직원·시설·이벤트 관리, 목표·결과 화면을 번역했습니다. 곡명, 손님 이름과 장식용 영문 간판은 고유 표기를 유지합니다.
- 한국어 글꼴은 `Resources/Fonts/NotoSansKR-Regular.otf`에 포함되어 있습니다. 라이선스는 같은 폴더의 `OFL.txt`입니다.
- 설정을 여는 동안 영업 시간은 멈추며, 닫기 또는 `P`로 이전 상태로 돌아갑니다. `P`의 기존 일시정지 기능도 사용할 수 있습니다.
- Play 모드에서 `SimBar > Validation > Check Running Localization`으로 언어 전환·저장·글꼴·일시정지 복원을 검증합니다.
- `글자 크기`는 기본·크게·아주 크게를 제공하며 기본값은 크게입니다. `SimBar.TextSize`에 영업 저장과 별도로 보관합니다. WebGL에서는 고밀도 화면의 픽셀 비율을 보정해 실제 표시 글자 크기를 유지합니다.
- 모바일 세로 화면은 하단 주문판, 가로 화면과 작은 PC 화면은 오른쪽 주문판을 사용합니다. 타이밍 서빙은 주문판과 같은 영역에서 전환하며, 카메라는 UI가 없는 별도 영역에서 매장을 보여줍니다. 목표·관리·음악·지도는 메뉴에서 엽니다. 넓은 PC 화면은 기존 주변 패널 구성을 유지합니다.
- 시작 화면에서도 매장이 보이며, 설정·안내·직원·시설·이벤트 화면은 실제 카드 크기로 재배치하고 고정 제목·주요 버튼과 스크롤 본문을 제공합니다.
- `SimBar > Validation > Check Running Readability`로 현재 Game View의 최소 표시 글자 크기, 크기 저장, 설정창 맞춤, 실제 게임 화면의 매장 영역·패널 겹침·버튼 도달성과 모바일 44px 터치 영역을 검사할 수 있습니다. 모바일 세로·가로와 PC 화면에서 각각 실행합니다.

## 프로젝트 개요

- 장르: 라이브 음악 펍 운영 시뮬레이션
- 화면: 아이소메트릭 룸, 따뜻한 브릭 바와 네온 공연장
- 핵심 루프: 3분 영업 시작 → 주문 선택·타이밍 서빙 → 콤보/FEVER → 목표·별점·XP 정산 → 다음 영업일
- 빌드 시작 씬: `Assets/Scenes/SimBar_Reference.unity` (기존 `SimBar_AutoScene`, `SampleScene`도 보존)

## 현재 구현

`Scripts/Game/RuntimeBootstrap.cs`가 씬 진입 시 `SimBarExperience`를 시작합니다. Blender에서 제작한 실제 환경 FBX와 프로젝트의 Punk·Casual·Suit FBX를 불러와 바, 부스, 4인 밴드, 춤추는 관객을 구성합니다. 에디터에서도 모델을 볼 수 있고 Play 진입 시 게임용 장면과 HUD를 생성합니다.

- 상단 HUD: 현금, 팬, 공연 시각
- 우측 패널: 곡 선택과 현재 재생 곡
- 하단 서비스 패널: 대기 주문, 인내심, 음료 제공 버튼
- 운영 패널: 손님 만족도, 공연 에너지, 실제 장면을 내려다보는 카메라 미니맵
- 3D 환경: 벽돌 벽, 크림 타일, 목재 카운터, 76개 술병과 청록 선반, 맥주탭, 부스, 식물, 포스터, 트러스, 스피커, 드럼셋
- 3D 인물: 기존 FBX 캐릭터를 실제 키로 정규화하고 Singing·Guitar Playing 동작을 연결. 손님 춤과 앉은 자세 지원
- 조명: 따뜻한 실내광, 그림자, 청록·분홍 무대 스포트라이트, 반투명 광선과 블룸
- 플레이 루프: 자동 주문, 맞는 음료에 보상, 틀린 음료와 주문 시간 초과에 만족도/팬 손실
- 직원 운영: 고용비와 주기 급여, 자동 서빙과 서비스·홍보 효과
- 시설 업그레이드: 단계별 구매와 상한, 음료 매출·팁·주문 수용·공연 효과
- 이벤트 운영: 개최비, 제한 시간, 진행 중 보너스, 완료 보상과 재개최 대기 시간
- 운영 저장: 현금·팬·직원·업그레이드·이벤트 진행을 로컬 PlayerPrefs에 보관
- 플레이어 여정: 시작/이어하기, 첫 영업 3단계 안내, Cozy/Challenge 모드, 영업 목표, 결과·재도전·다음 날
- 타이밍 서빙: 음료를 고른 뒤 SPACE·STOP·바 터치로 판정, Perfect/Good/Spill 피드백과 원본 효과음
- 성장: 수동 연속 성공 콤보, 12초 FEVER, 목표별 별점·영업 보너스·XP·최고 기록·랭크
- 배경음: 저작권 음원 없이 런타임에서 생성하는 짧은 신스 루프. 플레이리스트 항목마다 조성 변경

## 조작법

- 화면의 음료 버튼 클릭 또는 `1`, `2`, `3`: 주문에 맞는 음료를 선택하고 타이밍 게임 시작
- `E`: 주문에 맞는 음료를 자동 선택 (타이밍 판정은 플레이어가 직접 수행)
- `SPACE`, `STOP` 버튼 또는 타이밍 바 터치: 커서를 멈춰 음료 제공
- `P` 또는 좌측 상단 톱니 버튼: 일시정지/재개
- 우측 플레이리스트 항목 클릭: 곡과 신스 루프 변경
- `Staff Management`: 직원 고용과 급여 확인
- `Upgrades`, `Guest Info`, `Events`: 시설 구매, 손님/운영 현황, 이벤트 개최

운영 메뉴를 열면 주문 인내심·급여·이벤트 타이머가 일시정지됩니다. 닫기 또는 `P`로 메뉴 진입 전 상태로 돌아갑니다. 구매와 주요 운영 결과는 즉시 저장하며 플레이 중에도 주기적으로 저장합니다. Editor와 WebGL 브라우저의 저장 공간은 각각 독립적입니다.

## 처음부터 즐기는 플레이 흐름

1. 로비에서 `COZY NIGHT` 또는 `CHALLENGE NIGHT`를 고르고 `OPEN TONIGHT`를 누릅니다. 처음에는 시간 소모 없는 3단계 도움말을 보여줍니다.
2. 맨 앞 손님의 주문에 맞는 음료를 선택합니다. 3초 안에 움직이는 커서를 골드/그린 구간에서 멈춥니다. 골드는 Perfect, 그린은 Good이며, 구간 밖이나 시간 초과는 Spill입니다. Spill도 음료는 전달하지만 팁이 없고 콤보가 끊깁니다.
3. 수동 Good/Perfect를 연속으로 성공하면 콤보와 FEVER가 쌓입니다. 5회 연속 성공하고 공연 에너지가 72% 이상이면 12초간 팁 +75%, 점수 x1.3 효과를 받습니다. 틀린 음료·Spill·주문 시간 초과는 연속 성공과 FEVER를 끊습니다. 자동 직원은 주문 목표를 돕지만 수동 Perfect·콤보는 대신 만들지 않습니다.
4. 180초 후 영업이 종료되고 목표별 별점, 수익, 서빙 수, 영업 보너스, XP와 최고 기록을 보여줍니다. 진행 중인 이벤트도 이때 한 번 정산합니다.
5. `NEXT NIGHT`는 재산·직원·시설·XP를 유지하고 다음 날을 엽니다. `RETRY THIS NIGHT`는 같은 날을 새로 도전하되 재산을 초기화하거나 클릭 즉시 보상을 주지 않습니다.

| 모드 | 영업 시간 | 주문 목표 | 수동 Perfect 목표 | 최고 콤보 목표 | 그린 / 골드 폭 |
| --- | --- | ---: | ---: | ---: | --- |
| Cozy | 180초 | 12 | 4 | 5 | 40% / 16% |
| Challenge | 180초 | 20 | 7 | 8 | 28% / 9% |

Cozy는 손님 인내심 +18초와 느린 주문 유입, Challenge는 인내심 -12초와 빠른 주문 유입을 적용합니다. 목표 3개를 모두 달성하면 별 3개, 2개면 별 2개, 그 외에는 도전 보상으로 별 1개를 받습니다. 랭크는 누적 XP에 따라 Opening Act → Rising Star → Local Hero → Crowd Favorite → Headliner → House Legend로 성장합니다.

일시정지 화면의 `SAVE & LOBBY`로 남은 시간과 기록을 저장하고 로비로 돌아갈 수 있습니다. 진행 중인 영업이 있으면 모드를 바꾸거나 덮어쓰지 않고 `CONTINUE`로 이어갑니다. 다른 창/탭으로 나가면 자동 일시정지하며 돌아온 뒤 `P`나 재개 버튼으로 계속할 수 있습니다. 오프라인 시간은 차감하지 않습니다. 기존 운영 저장 데이터에 여정 정보가 없어도 현금·팬·직원·시설은 유지됩니다.

## 직원·시설·이벤트

기본 직원 2명 외에 아래 직원을 각각 1명씩 고용할 수 있습니다. 급여는 일시정지하지 않은 플레이 시간 60초마다 지급합니다. 잔액이 부족하면 추가 고용 직원이 업무를 중단하며, Staff 메뉴에서 밀린 급여를 정산하거나 자동 재시도 시점에 지급하면 복귀합니다.

| 직원 | 고용비 | 60초 급여 | 효과 |
| --- | ---: | ---: | --- |
| Barback | $450 | $45 | 12초마다 맨 앞 주문 자동 제공, Draught 업그레이드로 가속 |
| Floor Host | $600 | $35 | 새 주문 인내심 +18초, 만족도 회복 |
| Promoter | $750 | $50 | 20초마다 팬 +1, 주문 간격 10% 감소 |

| 시설 | 1 / 2 / 3단계 비용 | 단계당 효과 |
| --- | --- | --- |
| Draught System | $650 / $1,050 / $1,550 | 음료 매출 +8%, 자동 서빙 간격 12% 감소 |
| Sound & Lights | $750 / $1,250 / $1,800 | 팁 +10%, 공연 에너지 감소 완화 |
| VIP Lounge | $550 / $950 / $1,450 | 새 주문 인내심 +6초, 동시 주문 한도 +1 |

| 이벤트 | 개최비 / 시간 | 목표 주문 | 목표 달성 보상 | 종료 후 재개최 대기 |
| --- | --- | ---: | --- | --- |
| Open Mic Night | $180 / 60초 | 6잔 | $300 + 팬 12 | 90초 |
| Rock Showcase | $420 / 90초 | 9잔 | $650 + 팬 25 | 150초 |
| Fan Fest | $650 / 120초 | 12잔 | $1,050 + 팬 40 | 210초 |

이벤트는 동시에 1개만 진행합니다. 진행 중 매출·팁·팬·공연 보너스는 Events 메뉴에서 확인할 수 있습니다. 목표를 놓치면 성공 횟수는 오르지 않고 소규모 종료 보상 $50와 팬 3만 지급합니다. 자동 서빙도 이벤트 주문 실적에 포함됩니다. 재실행하면 저장된 남은 시간부터 재개되며 오프라인 시간은 차감하지 않습니다.

화면 중앙의 주문과 음료 이름이 일치할 때 제공해야 합니다. 빠르게 제공하면 더 많은 팁을 받고 팬이 증가합니다.

## 기존 게임 시스템과 데이터

- `GameManager.cs`: 현금, 팬(평판), 영업 상태
- `OrderManager.cs`, `Customer.cs`, `CustomerSpawner.cs`: 기존 주문/고객 AI 프로토타입
- `DrinkData.cs`, `Data/Order.cs`: 음료 및 주문 데이터
- `ScriptableObjects/Beer.asset`, `Cocktail.asset`, `Soda.asset`: 샘플 음료 데이터
- `BarSceneBuilder.cs`, `BarUIBuilder.cs`, `UIManager.cs`: 기존 씬/UI 프로토타입
- `Audio/AudioManager.cs`: 기존 오디오 매니저

현재 메인 플레이는 `SimBarExperience`의 화면 기반 서비스 루프를 사용합니다. 밴드의 FBX 애니메이션은 연결되어 있으며 NavMesh 고객 입장·퇴장 흐름은 후속 확장용 프로토타입입니다.

## Blender 원본과 재생성

- 편집 가능한 환경 원본: `ArtSource/Blender/SimBarVenue.blend`
- 편집 가능한 4인 밴드: `ArtSource/Blender/SimBarBand.blend`
- 밴드 교환용 FBX: `ArtSource/Blender/SimBarBand.fbx` (4개 리그, 74개 메쉬, 120프레임 동작 4개)
- 환경 FBX: `Assets/Resources/SimBarArt/SimBarVenue.fbx` (10개 mesh 그룹, 452,650 triangles)
- 원본 Gibson에서 경량화한 기타: `Assets/Resources/SimBarArt/Gibson_Stage.fbx` (28,465 triangles)
- 렌더 프리뷰와 모델 manifest: `ArtSource/Blender/`
- 원래 `Assets/Model/*.fbx` 파일은 보존합니다. 고용량 Gibson 원본은 경로만 기록하고 게임에서는 경량 모델을 사용합니다.

재생성 방법은 `ArtSource/Blender/README.md`에 있습니다. Unity 메뉴 `SimBar > Art > Import Blender Venue and Build Reference Scene`으로 재질·캐릭터 라이브러리·시작 씬을 다시 구성할 수 있습니다. 이 메뉴는 현재 씬에 저장하지 않은 변경이 있으면 씬 교체 전에 중단합니다.

기타·베이스는 기타 목이 위쪽을 향하도록 몸 앞 허리에 배치하고, 어깨를 지나는 스트랩과 양팔 보정을 사용합니다. 왼손은 지판을 따라 이동하고 오른손은 현 위에서 스트럼합니다. 기존 FBX 연주의 하체·몸통 움직임은 유지하며 보컬과 드럼은 별도 동작을 유지합니다.

## WebGL 실행

최신 플레이어 버전은 `Builds/SimBarPlayerWebGL`입니다. 이전 `Builds/SimBarOperationsWebGL`과 `Builds/SimBarReferenceWebGL`도 보존합니다. 압축 파일 헤더를 지원하는 미리보기 서버를 실행하고 브라우저에서 `http://127.0.0.1:8765/`를 엽니다.

```powershell
& 'E:/Blender/5.2/python/bin/python.exe' 'Tools/serve_simbar.py'
```

게임 화면 캡처는 `Assets/Screenshots/SimBar_Reference.png`, Blender 밴드 렌더는 `ArtSource/Blender/SimBarBand_preview.png`에 있습니다.

플레이어 흐름 캡처는 `Assets/Screenshots/SimBar_Journey_Lobby.png`, `SimBar_Journey_Guide.png`, `SimBar_Journey_Pour.png`, `SimBar_Journey_Fever.png`, `SimBar_Journey_Results.png`입니다.

## 검증

Reference 씬을 Play한 상태에서 `SimBar > Validation > Check Running Operations`와 `Check Running Guitar Poses`를 실행할 수 있습니다. 운영 검사는 비용·중복 방지·잔액 부족·최고 단계·자동 서빙·급여·이벤트·저장 복원·메뉴 일시정지를 검증한 뒤 검사 전 상태로 복원합니다. 기타 검사는 여러 시점의 손 위치, 팔 길이, 관절 각도, 손가락·엄지 그립과 어깨 스트랩을 확인합니다.

`SimBar > Validation > Check Running Player Journey`는 로비/도움말·타이밍 버튼·직원과 서빙 경쟁·콤보/FEVER·영업 종료·별/XP·저장 복원·재도전·정산 중복 방지·포커스 자동 일시정지를 검사한 뒤 원래 상태로 복원합니다. 테스트 중에는 운영 메뉴와 타이밍 게임을 먼저 닫아 주세요.

## 다음 확장 작업

- [x] 기존 FBX 인물·밴드 애니메이션과 Blender 환경 모델을 씬/프리팹에 연결
- [ ] NavMesh 고객 입장, 좌석 배정, 퇴장 흐름 연결
- [ ] 실제 효과음과 선택 가능한 라이선스 BGM 추가
- [x] 직원 고용, 시설 업그레이드, 이벤트 메뉴에 운영 기능 연결
- [x] 시작/이어하기, 타이밍 서빙, 콤보/FEVER, 영업 목표·결과와 성장 루프
- [ ] 성능 측정 후 군중 오브젝트 최적화 및 LOD 적용
