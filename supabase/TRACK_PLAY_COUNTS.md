# 음원 재생 횟수

대상은 `playlist.json`과 기존 `tracks`에 등록된 `track_001`~`track_005`입니다. MP3와 공개 URL은 기존 Cloudflare R2를 사용합니다. 새 재생 통계는 이 기능 적용 이후부터 집계하며, 이전 R2 파일 요청 수를 청취 횟수로 가져오지 않습니다.

## 집계 기준

- 한 재생 회차에서 실제 미디어 진행 시간이 누적 30초에 도달하면 1회입니다. 게임 시간이나 `play()` 호출 횟수는 사용하지 않습니다.
- 일시정지·버퍼링·세로 화면 전환 후 이어 듣기는 같은 회차입니다. 탐색으로 건너뛴 시간은 제외합니다.
- 다른 곡을 선택했다 돌아오거나 끝난 곡을 다시 재생하면 새 회차 UUID를 사용합니다. 자동 다음 곡은 같은 Audio 요소를 재사용해도 새 UUID입니다.
- 집계 전송 재시도와 응답 유실은 같은 UUID로 처리해 한 번만 증가합니다. 자격을 얻은 요청만 로컬 큐에 저장하고 최대 100개·7일 범위에서 재전송합니다. 오프라인 기록은 연결 복구 후 반영될 수 있습니다.
- 재생 목록은 서버가 확인한 합계를 곡 제목 뒤에 표시합니다. 조회 실패는 0회로 표시하지 않습니다. 제목과 횟수를 기존 한 줄 마퀴로 함께 이동시킵니다. 목록 진입·화면 복귀·온라인 복귀 및 목록이 열린 상태의 60초 간격으로 합계를 갱신합니다.

## 저장소와 API

| 위치 | 역할 | 클라이언트 권한 |
| --- | --- | --- |
| Cloudflare R2 | 기존 5개 MP3 전송 | 기존 공개 스트리밍 |
| `public.track_play_counts` | 곡 ID별 누적 횟수·갱신 시각 | 활성 곡의 합계 SELECT만 허용 |
| `simbar_private.music_play_events` | 집계된 회차 UUID·곡 ID·웹/Windows·30초 증거·집계 시각 | 접근 불가. RLS의 클라이언트 정책을 두지 않아 기본 거부 |
| `public.record_music_play` | 중복 원장 삽입과 합계 증가를 한 트랜잭션으로 처리 | `service_role` 전용, `SECURITY INVOKER` |
| Edge Function `track-play` | 시작 티켓 발행·검증 후 서버 전용 RPC 호출 | 공개 publishable API key 검증 |

원장에는 사용자 ID·IP·이메일·기기 식별자를 저장하지 않습니다. 합계는 재생 회차 수이며 고유 청취자 수가 아닙니다. 클라이언트 관측을 집계하는 앱 통계이므로 실제 사람의 청취나 부정 재생 방지를 보증하는 지표로 사용하지 않습니다.

`start` 요청은 DB 행을 만들지 않고 회차·곡·시작 시각·플랫폼에 연결된 HMAC 티켓을 반환합니다. `qualify`는 티켓 발급 후 서버 기준 30초 경과와 클라이언트 청취 30초 이상을 확인합니다. 티켓은 24시간 후 만료되며, 만료·오프라인 복구 시 새 티켓의 대기 시간이 추가될 수 있습니다. 유효한 집계만 원자적으로 기록합니다.

Edge Function은 `SUPABASE_PUBLISHABLE_KEYS`의 `default` 키를 직접 검증하므로 `verify_jwt = false`입니다. 사용자의 로그인과 익명 Auth 가입은 필요하지 않습니다. 서버는 자동 주입된 `SUPABASE_SECRET_KEYS`의 `default` 또는 서버의 레거시 service-role 키만 사용합니다. 브라우저·Windows 패키지에는 프로젝트 URL과 공개 publishable key만 포함합니다. 서비스 비밀 키를 프런트엔드나 Git에 넣지 않습니다.

HMAC는 서버 키에서 목적을 구분해 파생합니다. 필요하면 서버 비밀 `TRACK_PLAY_TOKEN_SECRET`으로 별도 회전할 수 있습니다. CORS는 공개 게임의 GitHub Pages origin, `simbar://app`, 로컬 개발 origin을 허용합니다.

## 검증

`Tools/test_native_play_tracker.mjs`는 30초 경계·일시정지·버퍼링·탐색·자동 다음 곡·재재생·이전 이벤트를 확인합니다. `Tools/test_native_play_stats.mjs`는 큐 복원·재전송·응답 유실·티켓 만료·조회 실패·권한 경계를 확인합니다. 원격 검증은 트랜잭션 롤백 또는 테스트 UUID에 한정한 원장 삭제·동일 건수 차감으로 테스트 재생이 실제 합계에 남지 않도록 처리합니다.

공식 참고: [Supabase 함수 API 키 인증](https://supabase.com/docs/guides/functions/auth-headers), [서버 환경 변수](https://supabase.com/docs/guides/functions/secrets), [R2 통계](https://developers.cloudflare.com/r2/platform/metrics-analytics/).
