# SimBar

Unity 6000.6.3f1로 만든 라이브 음악 펍 운영 게임입니다.

**게임 실행: https://jtsgrit0.github.io/simbar_63/**

로비와 게임 화면의 설정 메뉴에서 **한국어 / English**를 선택할 수 있습니다. 선택한 언어와 게임 진행 상황은 브라우저에 저장됩니다.

게임은 브라우저 창 크기에 맞춰 확장됩니다. 설정의 **글자 크기**에서 기본·크게·아주 크게를 선택할 수 있으며, 기본값은 크게입니다. 작은 화면에서는 **음악**, **지도** 버튼으로 보조 패널을 펼쳐 볼 수 있습니다. 안내·관리 화면은 글자 크기를 유지하며 스크롤할 수 있습니다.

## 조작

- `1`, `2`, `3`: 주문에 맞는 음료 선택
- `SPACE` 또는 `STOP` 버튼: 타이밍에 맞춰 음료 따르기
- `E`: 손님이 주문한 음료 선택
- `P`: 일시정지 / 재개, 설정 창 닫기
- 톱니바퀴 또는 `설정`: 언어 선택
- 설정의 `글자 크기`: 즉시 적용되는 크기 선택과 저장
- 웹 페이지의 `전체 화면 / Full screen`: 화면 확장·복귀

## 프로젝트 실행

Unity Hub에서 이 폴더를 Unity **6000.6.3f1**로 열고 `Assets/Scenes/SimBar_Reference.unity`를 실행합니다. 게임 구성과 검증 방법은 [Assets/README_SIMBAR.md](Assets/README_SIMBAR.md)에 있습니다.

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
