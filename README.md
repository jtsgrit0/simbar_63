# SimBar

Unity 6000.6.3f1로 만든 라이브 음악 펍 운영 게임입니다.

**게임 실행: https://jtsgrit0.github.io/simbar_63/**

로비와 게임 화면의 설정 메뉴에서 **한국어 / English**를 선택할 수 있습니다. 선택한 언어와 게임 진행 상황은 브라우저에 저장됩니다.

영업 중 UI는 첫 배포(`f34743e`)의 HUD·글자·창 배치를 사용합니다. 상단의 현금·팬·시각, 오른쪽 재생 목록, 하단 주문·서빙과 주변 관리 패널을 유지하며 한국어·영문으로 전환할 수 있습니다.

시작 타이틀에서는 여성 사장, FF 사장, 펫사운즈 사장 세 명 중 하나를 선택합니다. 선택한 사장은 매장 저장과 별도의 `SimBar.Owner` 설정에 보관합니다. 새 타이틀만 화면 크기에 맞춰 같은 비율로 축소하며, [사장 일러스트 제작 기록](ArtSource/Owners/README.md)에 그림의 출처와 생성 프롬프트를 보관합니다.

관객은 밴드와 다른 6종 모델에 의상·바지·헤어 색상 조합을 사용합니다. 무대 앞에서는 6명이 서클핏 모싱을 하고 2명이 짝을 지어 슬램 동작을 합니다. 별도의 서핑 팀은 받치는 3명과 서퍼 1명으로, 24초 주기로 들어 올리고 운반한 뒤 내려놓습니다.

슬램 두 사람은 몸 간격 0.84m에서 서로 접촉하고 뒤로 물러납니다. 연속 이동 경로의 몸 충돌을 검사해 상대를 통과하지 않으며, 모슁은 원호를 따라 움직입니다. 주문을 받으러 가는 공연 관객도 다른 사람의 몸과 슬램 구역을 피해 이동합니다.

벽의 아래 1/3은 짙은 회색이고, 위쪽은 여닫이 유리창 너머로 서울 밤의 네온사인 거리가 보입니다. 바닥은 벽보다 밝은 회색 콘크리트로 이어지며 갈색 타일 격자는 제거했습니다. [서울 야경 자산과 제작 안내](ArtSource/Seoul/README.md)에 원본 정보를 보관합니다.

화분 네 개를 없애고 같은 위치에 회색 사각 기둥을 세웠습니다. 창 아래의 짙은 회색 벽에는 공연 포스터 16장을, 기둥 면에는 6장을 붙였습니다. Queen·Nirvana·The Beatles·Taylor Swift·BLACKPINK·POP NIGHT 그림 6종을 낡은 종이와 테이프, 비정형 겹침으로 배치해 런던 펍의 그런지 콜라주 분위기를 유지합니다. 벽 콜라주는 유리창 아래에 붙어 서울 야경을 가리지 않습니다. [포스터 자산 안내](ArtSource/Posters/README.md)와 [기둥 제작 안내](ArtSource/Blender/GRAY_COLUMNS.md)를 함께 보관합니다.

회색 바닥 전체에는 아티스트 이름과 스프레이 자국을 비대칭으로 흩어 배치했습니다. Queen은 골드, Nirvana는 오렌지, The Beatles는 민트, Taylor Swift는 라벤더, BLACKPINK는 핑크로 구분합니다. [그래피티 자산과 생성 프롬프트](ArtSource/Graffiti/README.md)에서 색상과 제작 정보를 확인할 수 있습니다.

각 타이틀 주변과 빈 바닥에는 [불규칙한 페인트 방울과 잔분사 자국](ArtSource/Graffiti/Splatter.md)을 더했습니다. 얼룩의 크기·방향·밀도를 섞어 비대칭 무리와 작은 꼬리로 흩뿌리며, 글자 중심과 가구 주변은 피합니다.

주문 손님은 24명입니다. 무대 앞 8명도 주문이 생기면 피트에서 나와 바 서비스를 받은 뒤 돌아가며, 추가 서핑 팀 4명은 공연 연출만 담당합니다. 일반 홀 손님은 짧게 이동한 뒤 느슨하게 쉬고, 바 손님 4명은 스툴에 앉아 음료를 마십니다. 바텐더 동선·밴드 동작·무대 리듬 조명은 영업 중에만 진행하며 일시정지·로비·관리 화면에서는 멈춥니다. 기존 주문 순서와 서빙·보상 규칙은 유지됩니다.

## 조작

- `1`~`5`: 주문에 맞는 메뉴 선택
- `M`: 바 주문 서비스 목록
- `H` 또는 화면 오른쪽 `공연·홀 미션`: 꽃가루·휴지 퍼포먼스, 공연 후 청소 배치, 만취 손님 안내
- `SPACE` 또는 `STOP` 버튼: 타이밍에 맞춰 음료 따르기
- `E`: 손님이 주문한 음료 선택
- `P`: 일시정지 / 재개, 설정 창 닫기
- 톱니바퀴 또는 `설정`: 언어 선택
- 웹 페이지의 `전체 화면 / Full screen`: 화면 확장·복귀

공연 중 꽃가루와 휴지를 각각 두 번 뿌릴 수 있습니다. 팬과 공연 분위기가 올라가고 바닥에는 종이가 남습니다. 3분 공연이 끝나면 주문을 멈추고, 기존 직원에게 청소를 배정합니다. 직원이 실제로 청소 구역에 도착해 작업해야 정리량이 줄며, 완료 보너스 ₩70,000은 하루 정산에서 한 번 지급합니다.

공연 중 나타나는 만취 손님은 추가 주문 중단·물 안내, 차분한 퇴점 요청, 소지품 확인·직원 동행 순서로 정중하게 안내합니다. 손님 안내와 바닥 정리가 끝난 뒤 정산하며, 진행 중 저장하고 이어할 수 있습니다. 홀 미션 창은 공연 중 시간을 멈추고, 공연 후에는 직원 작업이 계속됩니다.

## 프로젝트 실행

Unity Hub에서 이 폴더를 Unity **6000.6.3f1**로 열고 `Assets/Scenes/SimBar_Reference.unity`를 실행합니다. 게임 구성과 검증 방법은 [Assets/README_SIMBAR.md](Assets/README_SIMBAR.md)에 있습니다.

Play 모드에서 `SimBar > Validation > Check Running Original UI`로 원본 UI 배치를, `Check Running Motion`으로 60초 동선·밴드·관객 연출을, `Check Running Owner Title`로 사장 선택·저장·한영 전환을 검증할 수 있습니다.

## 웹 배포

GitHub Pages는 `main` 브랜치의 `docs/` 폴더를 게시합니다. Unity에서 `Web - Mobile - Release` 또는 `Web - Desktop - Release` 프로필로 `Builds/Pages/SimBarPlayerWebGL`에 빌드한 후 다음 명령으로 게시용 파일을 갱신합니다. 릴리스 프로필은 실제 게임 장면만 포함하며, Unity 시작 로고를 표시하지 않습니다.

```powershell
python Tools/prepare_github_pages.py --source Builds/Pages/SimBarPlayerWebGL
```

릴리스 프로필은 Unity 파일 압축과 `Decompression Fallback`을 끕니다. GitHub Pages의 HTTP gzip 압축은 브라우저가 처리하고, WebAssembly는 내려받는 동안 컴파일합니다. JavaScript Brotli 압축 해제로 빠른 회선·재접속의 시작이 늦어지는 것을 방지합니다. 다른 호스팅에서도 `.wasm`은 `application/wasm`으로 제공하고 HTTP 압축을 켜 주세요. 패키징 스크립트는 이전 `.unityweb` 빌드도 그대로 보존하며, `.br`/`.gz` 빌드는 압축을 해제해 배포합니다.

시작에 사용하는 그림 원본은 `Assets/Art/StartupTextures/`에 보관합니다. 빌드 전에 같은 해상도의 JPEG(불투명 이미지) 또는 PNG(투명 이미지) 데이터를 `Assets/Resources/SimBarWebTextures/*.bytes`로 자동 생성하고, 실행 중 한 번만 디코딩해 공유합니다. 인코더는 해상도·색상 오차·투명도를 검증합니다. 중복 프로토타입 모델은 `Assets/Legacy/`에 보관하고, 실제 캐릭터와 보행 호환 참조는 원본 모델을 재사용합니다. 원본 FBX 파일을 유지하면서 메시 압축을 적용하고, 릴리스 프로필은 필요한 저장 데이터의 필드를 보존하면서 사용하지 않는 관리 코드를 제거합니다. 웹 플레이어는 사용하지 않는 프로토타입 UI·게임 관리자를 생성하지 않습니다.

반응형 웹 템플릿은 `Tools/WebGLTemplate/`에 보관합니다. 웹 페이지의 HTML·CSS만 바꿀 때는 `python Tools/prepare_github_pages.py --host-only`로 게시 파일을 갱신할 수 있습니다. 릴리스 빌드는 파일 내용의 해시를 파일명에 사용해 새 빌드와 이전 캐시가 섞이지 않도록 하며, 같은 버전의 데이터와 WebAssembly 응답은 브라우저의 Cache Storage에서 재사용합니다. 캐시를 읽어도 WebAssembly 스트리밍 컴파일을 유지하며, 캐시 쓰기 완료를 기다리지 않고 게임을 시작합니다.

`docs/` 변경 사항을 커밋하여 `main`에 푸시하면 GitHub Pages가 갱신됩니다. Unity 캐시, 로컬 빌드, 임시 복구 파일은 Git에 포함하지 않습니다.

실제 웹 접속 성능은 `uv run --with playwright python Tools/benchmark_web_loading.py --url https://jtsgrit0.github.io/simbar_63/ --require-game-ready --samples 3`으로 확인합니다. 새 브라우저 컨텍스트에서 첫 접속과 재접속을 측정하며, 기본값은 네트워크 제한 없음입니다. `simbarGameReadyAt`은 실제 게임 UI를 그린 뒤 기록하므로 Unity 로고나 로딩 HTML을 실행 완료로 세지 않습니다. 세부 결과와 화면은 `Temp/perf/`에 저장합니다.

## 포함 자산

서드파티 자산은 각 폴더의 라이선스 및 원래 사용 조건을 따릅니다. 한글 글꼴은 [SIL Open Font License](Assets/Resources/Fonts/OFL.txt)를 사용합니다.
