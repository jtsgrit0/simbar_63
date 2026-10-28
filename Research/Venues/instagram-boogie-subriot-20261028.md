# 부기우기 / 서브라이엇 — 포스터 이미지 확인

확인일: 2026-10-28. Instagram 계정을 검색했으나 원 프로필/게시물의 직접 열람은 접근 제한 또는 전송 오류로 완료하지 못했다. 실제로 본 이미지는 아래 공개 공연 일정 사이트의 포스터 사본이다. 로그인 우회는 하지 않았다.

## 부기우기

- 계정: [@boogiewoogieseoul](https://www.instagram.com/boogiewoogieseoul/). 포스터 하단에 같은 Instagram ID가 인쇄되어 있다. [서울관광재단](https://korean.visitseoul.net/editorspicks/%ED%95%B4%EB%B0%A9%EC%B4%8C-%EB%9D%BC%EC%9D%B4%EB%B8%8C%EB%B0%94/KON041859)과 [공식 홈페이지](https://www.bgwg.kr/)의 장소 식별을 대조했다.
- 포스터 출처: https://muzikey.com/events/b28ec687-4ec9-4336-a989-8f27662c3753
- 원본 이미지: https://dagkmgabjviughtwsofn.supabase.co/storage/v1/object/public/events/b28ec687-4ec9-4336-a989-8f27662c3753/poster.webp
- 실제 확인한 그림: 갈색 바탕의 세로형 `JAZZ LIVE SHOW`, 10월 13일 월요일부터 19일 일요일까지의 주간 표. 하단 Boogie Woogie 로고, 회나무로 21 2층, Instagram ID가 있다.
- 연도 근거: 이미지에는 월·일·요일이 있고 공개 이벤트의 `startDate/event:start_date`는 `2025-10-17T11:00:00.000Z`다. 따라서 2025년 10월 13~19일 포스터로 기록한다. 2026년 10월 일정으로 혼동하지 않는다.
- 게임에 선택한 두 출연진: **yeastrio**(10/13 18–20시), **OKLP GROUP**(10/13 20–22시). 두 이름을 이미지의 왼쪽 첫 날짜 구역에서 직접 읽었다. 둘 다 이미지가 Standard Jazz를 포함한 Jazz 범주를 명시한다. 상세 세부 장르를 하나로 단정하지 않고 게임에서는 Jazz로 표시한다.
- 전체 포스터에는 더 많은 출연자가 있으나 이번 게임 카탈로그에서는 일부만 선택했다. 개별 원 Instagram 게시물 URL은 확인하지 못했다.
- 앞서 발견한 Instagram 미러의 `De7MGwzE6bR` / `De85wcjkyXi` 이미지는 640px 정사각형 상단 자르기여서 출연명이 보이지 않았다. **그 이미지의 캡션만으로 출연진을 전사하지 않았고 게임 자료로 채택하지 않았다.**

## 해방촌 서브라이엇

- 계정: [@subriot_hbc](https://www.instagram.com/subriot_hbc/). [공식 Linktree](https://linktr.ee/subriothbc)의 연결과 포스터 하단의 `@SUBRIOT_HBC`를 대조했다. `@SUB_RIOT` 커뮤니티와 혼동하지 않는다.
- 포스터 출처: https://muzikey.com/events/4cab399e-3ddf-47e6-93a0-5b9886c4ece7
- 원본 이미지: https://dagkmgabjviughtwsofn.supabase.co/storage/v1/object/public/events/4cab399e-3ddf-47e6-93a0-5b9886c4ece7/poster.webp
- 실제 확인한 그림: 아이보리·초록색 `SUBRIOT HBC JAZZ LIVE!` 세로 포스터. 하단에 **2025.12.12–12.14**가 명시되고 신흥로7길 1 지하 1층 주소와 계정명이 있다.
- 게임에 선택한 두 출연진: **Lagrangian**(12/12 21–23시, Fusion Jazz / Contemporary Jazz), **Newsboy Men**(12/13 18–20시, Blues / Vocal). 이름·시간·장르를 포스터에서 직접 읽었다.
- 전체 표의 나머지 이름은 자동으로 게임 후보로 등록하지 않았다. 개별 Instagram 게시물 URL은 확인하지 못했다. 공연 공지는 실제 개최 완료를 보장하지 않는다.

## 게임 자료 처리

다운로드 URL·이미지 크기·원 파일 해시·게임 파일 해시는 `scout-poster-sources.json`과 `scout-poster-integrity.json`에 있다. JPEG는 바이트 그대로 보관했고 WebP는 Unity가 읽을 수 있도록 **픽셀을 바꾸지 않는 PNG 형식 변환만** 했다. 자르기·리사이즈·색 변경·문자 교체는 하지 않았다. 게임은 이미지를 StreamingAssets에서 포스터 열람 시에만 내려받는다.

실명·행사 날짜·출처와 게임의 출연료·수락 확률·관객 동원·관계·성장은 서로 다른 데이터다. 후자는 모두 가상 게임 값이다. 아티스트의 실제 섭외 가능 여부나 신인 여부를 확인한 것으로 표시하지 않는다.
