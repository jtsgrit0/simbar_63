# SimBar

Unity 6000.6.3f1로 만든 라이브 음악 펍 운영 게임입니다.

**게임 실행: https://jtsgrit0.github.io/simbar_63/**

로비와 게임 화면의 설정 메뉴에서 **한국어 / English**를 선택할 수 있습니다. 선택한 언어와 게임 진행 상황은 브라우저에 저장됩니다.

UI는 첫 배포(`f34743e`)의 HUD·글자·창 배치를 사용합니다. 상단의 현금·팬·시각, 오른쪽 재생 목록, 하단 주문·서빙과 주변 관리 패널을 유지하며 한국어·영문으로 전환할 수 있습니다.

관객은 밴드와 다른 6종 모델에 의상·바지·헤어 색상 조합을 사용합니다. 무대 앞에서는 6명이 서클핏 모싱을 하고 2명이 짝을 지어 슬램 동작을 합니다. 별도의 서핑 팀은 받치는 3명과 서퍼 1명으로, 24초 주기로 들어 올리고 운반한 뒤 내려놓습니다.

벽의 아래 1/3은 짙은 회색이고, 위쪽은 여닫이 유리창 너머로 서울 밤의 네온사인 거리가 보입니다. 바닥은 벽보다 밝은 회색 콘크리트로 이어지며 갈색 타일 격자는 제거했습니다. [서울 야경 자산과 제작 안내](ArtSource/Seoul/README.md)에 원본 정보를 보관합니다.

창 아래와 짙은 회색 벽에는 런던 라이브 펍처럼 낡은 공연 종이를 작게 겹쳐 붙인 그런지 콜라주가 남아 있습니다. Queen·Nirvana·The Beatles·Taylor Swift·BLACKPINK·POP NIGHT 그림 6종을 뒤벽과 왼벽에 각각 8장씩, 총 16장 배치하고 기울기·크기·높이를 달리했습니다. 종이와 테이프의 윗끝을 2.35m 아래로 낮춰 창밖 풍경을 열어 두었습니다. [포스터 생성 프롬프트와 자산 안내](ArtSource/Posters/README.md)를 함께 보관합니다.

회색 바닥 전체에는 아티스트 이름과 스프레이 자국을 비대칭으로 흩어 배치했습니다. Queen은 골드, Nirvana는 오렌지, The Beatles는 민트, Taylor Swift는 라벤더, BLACKPINK는 핑크로 구분합니다. [그래피티 자산과 생성 프롬프트](ArtSource/Graffiti/README.md)에서 색상과 제작 정보를 확인할 수 있습니다.

각 타이틀 주변과 빈 바닥에는 [불규칙한 페인트 방울과 잔분사 자국](ArtSource/Graffiti/Splatter.md)을 더했습니다. 얼룩의 크기·방향·밀도를 섞어 비대칭 무리와 작은 꼬리로 흩뿌리며, 글자 중심과 가구 주변은 피합니다.

주문 손님은 24명입니다. 무대 앞 8명도 주문이 생기면 피트에서 나와 바 서비스를 받은 뒤 돌아가며, 추가 서핑 팀 4명은 공연 연출만 담당합니다. 일반 홀 손님은 짧게 이동한 뒤 느슨하게 쉬고, 바 손님 4명은 스툴에 앉아 음료를 마십니다. 바텐더 동선·밴드 동작·무대 리듬 조명은 영업 중에만 진행하며 일시정지·로비·관리 화면에서는 멈춥니다. 기존 주문 순서와 서빙·보상 규칙은 유지됩니다.

## 조작

- `1`, `2`, `3`: 주문에 맞는 음료 선택
- `SPACE` 또는 `STOP` 버튼: 타이밍에 맞춰 음료 따르기
- `E`: 손님이 주문한 음료 선택
- `P`: 일시정지 / 재개, 설정 창 닫기
- 톱니바퀴 또는 `설정`: 언어 선택
- 웹 페이지의 `전체 화면 / Full screen`: 화면 확장·복귀

## 프로젝트 실행

Unity Hub에서 이 폴더를 Unity **6000.6.3f1**로 열고 `Assets/Scenes/SimBar_Reference.unity`를 실행합니다. 게임 구성과 검증 방법은 [Assets/README_SIMBAR.md](Assets/README_SIMBAR.md)에 있습니다.

Play 모드에서 `SimBar > Validation > Check Running Original UI`로 원본 UI 배치를, `Check Running Motion`으로 60초 동선·밴드·관객 연출을 검증할 수 있습니다.

## 웹 배포

GitHub Pages는 `main` 브랜치의 `docs/` 폴더를 게시합니다. Unity WebGL 빌드를 `Builds/SimBarPlayerWebGL`에 만든 후 다음 명령으로 게시용 파일을 갱신합니다.

```powershell
python Tools/prepare_github_pages.py
```

이 스크립트는 Brotli 압축을 해제해 일반 정적 호스팅에서도 실행되는 파일을 만듭니다. Windows에서는 Unity에 포함된 Brotli 실행 파일을 사용하며, 다른 환경에서는 Python `brotli` 패키지 또는 `--brotli` 옵션으로 지정한 실행 파일을 사용할 수 있습니다.

반응형 웹 템플릿은 `Tools/WebGLTemplate/`에 보관합니다. 웹 페이지의 HTML·CSS만 바꿀 때는 `python Tools/prepare_github_pages.py --host-only`로 게시 파일을 갱신할 수 있습니다. 자산 주소에 파일 해시를 붙여 새 빌드와 이전 캐시가 섞이지 않도록 합니다.

`docs/` 변경 사항을 커밋하여 `main`에 푸시하면 GitHub Pages가 갱신됩니다. Unity 캐시, 로컬 빌드, 임시 복구 파일은 Git에 포함하지 않습니다.

## 포함 자산

서드파티 자산은 각 폴더의 라이선스 및 원래 사용 조건을 따릅니다. 한글 글꼴은 [SIL Open Font License](Assets/Resources/Fonts/OFL.txt)를 사용합니다.
