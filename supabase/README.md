# SIMBAR 참여형 공연장 DB

대상 프로젝트는 [`ziyosbcfdsbirhdsgxax`](https://supabase.com/dashboard/project/ziyosbcfdsbirhdsgxax)입니다. 이 폴더는 **공연장 꾸미기·공유·방문**을 위한 DB 기반을 구성합니다. Gemini 대화 본문은 아직 확인하지 못했으므로, 확인된 참여 범위를 기준으로 작성했습니다.

현재 적용 상태: **원격 DB·카탈로그·RLS·RPC 적용 및 검증 완료**. 연결된 프로젝트는 `SimBar`(`ziyosbcfdsbirhdsgxax`), 리전은 `ap-south-1`, PostgreSQL 버전은 `17.11.0.018`입니다. 기존 게임의 로그인·클라우드 저장 화면 연결과 일부 호스팅 Auth 설정은 남아 있습니다.

| 구분 | 확인된 상태 |
| --- | --- |
| 프로젝트 접근 | Supabase 연결 도구의 OAuth 인증 완료 |
| DB | 적용 전 비어 있던 `public`에 테이블 13개, 공개 RPC 13개, 비공개 보조 함수 2개 구성 |
| 권한 | 13개 테이블 모두 RLS 활성화. 클라이언트 직접 INSERT·UPDATE·DELETE 및 함수의 `PUBLIC` 실행 권한 없음 |
| 카탈로그 | 밴드 2팀, 기존 R2 음원 5곡, 무료 꾸미기 아이템 4개 등록 |
| 로컬 연결 | Git에서 제외되는 `.env`에 프로젝트 URL·공개 publishable key 저장. 관리 비밀 키 저장 없음 |
| Auth 확인 | 가입·이메일 로그인·이메일 확인 활성화, 익명 계정 생성 비활성화 |
| Auth 남은 설정 | 호스팅 사이트 URL·리디렉션 허용 목록·최소 비밀번호 8자 값은 아직 적용·검증하지 못함 |

`config.toml`은 설정 목표를 담은 파일입니다. 저장만으로 호스팅 Auth 설정이 바뀌지는 않습니다. 현재 연결 도구에는 Auth 설정 변경 기능이 없고 CLI는 별도 인증이 필요합니다. [Auth URL 설정](https://supabase.com/dashboard/project/ziyosbcfdsbirhdsgxax/auth/url-configuration)과 [인증 제공자 설정](https://supabase.com/dashboard/project/ziyosbcfdsbirhdsgxax/auth/providers)에서 아래 목표 값과 맞춰야 합니다.

## 테이블 구성

| 테이블 | 저장하는 내용 | 사용 예와 접근 범위 |
| --- | --- | --- |
| `profiles` | Supabase Auth 사용자와 연결된 닉네임 등 프로필 | 로그인 계정과 공연장 소유자를 연결. 이메일·비밀번호는 `auth.users`에서 관리 |
| `bands` | 프리윌·줄라이원츠모어 등 밴드 정보 | 공연장 테마와 곡·포스터를 묶는 공개 카탈로그 |
| `media_assets` | 파일 종류, 저장소, 객체 키, 공개 URL | MP3 본문 대신 기존 R2 주소를 저장 |
| `tracks` | 고정 곡 ID, 밴드, 제목, 재생 시간, 음원 참조 | `track_001`~`track_005` 순서와 ID 유지 |
| `room_templates` | 기본 공간, 좌표 범위, 배치 구역, 버전 | 작은 공연장 템플릿과 최대 배치 수 |
| `items` | 포스터·가구·조명 카탈로그와 허용 구역 | 벽 포스터는 `wall`, 소파는 `floor`에 배치 |
| `user_inventory` | 사용자가 획득한 아이템 | 무료 아이템 외 소유권 검증. 본인만 조회 |
| `rooms` | 공연장 주인, 제목, 선택한 밴드, 공개본 연결 | 소유자가 여러 공연장을 만들 수 있음 |
| `room_layouts` | 공연장별 저장된 꾸미기 상태와 수정 버전 | 같은 공연장에 여러 프리셋 저장. 본인만 조회 |
| `room_placements` | 프리셋 안 아이템·구역·좌표·방향·옵션 | 서버가 아이템 소유 여부와 배치 범위 검증 |
| `layout_tracks` | 프리셋별 곡 목록과 순서 | 손으로 적은 셋리스트 등에 연결할 재생 순서 |
| `room_publications` | 발행 당시의 레이아웃 스냅샷과 공유 슬러그 | 이후 초안을 수정해도 공개본은 유지. 공개 API로 필요한 필드만 제공 |
| `room_visits` | 공개본 방문 세션과 방문 시간 | 동일 공개본·세션 중복 집계 방지. 원시 기록은 방문자에게 공개하지 않음 |

초안은 소유자만 읽고 수정합니다. 저장·발행·방문 기록은 RPC로 처리하며, 클라이언트가 소유자 ID나 방문 수를 직접 바꾸지 못하도록 제한합니다. 공유 링크로 보여주는 데이터는 발행 시점의 스냅샷입니다. 링크 한정 공개는 슬러그를 정확히 알아야 조회할 수 있고, 공개 목록에는 노출하지 않습니다.

## 클라이언트에서 호출할 함수

| RPC | 용도 |
| --- | --- |
| `update_profile(p_display_name)` | 본인 닉네임 변경 |
| `create_room(p_template_id, p_title, p_band_id)` | 공연장과 기본 배치를 만들고 공연장 UUID 반환 |
| `rename_room(p_room_id, p_title)` / `delete_room(p_room_id)` | 본인 공연장 이름 변경·삭제 |
| `create_layout(p_room_id, p_name)` | 공연장에 배치 프리셋 추가, 최대 20개 |
| `rename_layout(p_layout_id, p_name)` / `delete_layout(p_layout_id)` | 배치 이름 변경·삭제. 마지막 배치와 현재 공개 중인 배치는 삭제 제한 |
| `save_layout(p_layout_id, p_expected_revision, p_placements, p_tracks, p_settings)` | 전체 배치와 셋리스트를 한 번에 저장하고 새 수정 버전 반환 |
| `publish_room(p_room_id, p_expected_revision, p_visibility, p_layout_id)` | 선택한 배치 발행. 프리셋이 여러 개면 배치 UUID 필수. 공유 슬러그 반환 |
| `unpublish_room(p_room_id)` | 공유 중단. 저장된 초안과 발행 이력 유지 |
| `get_published_room(p_slug)` | 현재 공유 중인 공연장 조회. 찾을 수 없으면 `null` |
| `list_public_rooms(p_limit, p_offset)` | 공개 목록 조회. `unlisted`는 제외 |
| `record_visit(p_slug, p_session_id)` | 공개본·세션별 방문 기록. 반환 값은 누적 세션 수 |

`save_layout`의 `p_placements`는 `[{"key":"poster-1","item_id":"poster_freewill","zone":"wall","x":15,"y":25,"rotation":0,"scale":1,"options":{"flip_x":false}}]`, `p_tracks`는 `["track_001","track_004"]` 형태입니다. `p_settings`는 `lighting`(`warm`/`cool`/`neutral`), `ambience`(`quiet`/`crowd`)를 선택적으로 받습니다. 좌표는 템플릿 범위 안이어야 합니다. 소유권은 로그인 JWT에서 얻으며 사용자 ID를 입력받지 않습니다.

버전이 오래된 저장·발행 요청은 SQLSTATE `40001`로 거절하므로 현재 초안을 다시 읽고 사용자 변경을 확인해야 합니다. 발행본은 제목·닉네임·템플릿 정의·아이템 표시 정보·셋리스트를 복사해 보관합니다. 변경한 내용을 공유하려면 다시 발행합니다. 재발행과 공개 중단 뒤에는 이전 공유 슬러그가 조회되지 않습니다. 공연장 삭제는 배치·발행 이력·방문 기록까지 삭제하며 복구 기능은 제공하지 않습니다.

동시 접속자 수에 맞춘 실시간 위치 동기화·채팅·매치메이킹은 이 DB 범위에 포함하지 않았습니다. 방문 수는 중복을 제거한 **세션 수**이며, 사람 단위의 검증된 순방문자 수는 아닙니다.

## 초기 데이터

`seed.sql`에는 두 밴드, 기존 R2 음원 5곡의 메타데이터, `starter_club` 템플릿, 무료 포스터 2종·소파·조명이 들어 있습니다. 새 MP3 업로드는 하지 않습니다. 꾸미기 아이템의 실제 아트 URL은 승인된 이미지가 준비될 때 등록하도록 비워 두었습니다.

| 곡 ID | 밴드 | 곡 |
| --- | --- | --- |
| `track_001` | 프리윌 | Electric Shocker |
| `track_002` | Julywantsmore | Hey |
| `track_003` | Julywantsmore | No Matter How |
| `track_004` | 프리윌 | Bedroom Manifesto |
| `track_005` | 프리윌 | Electric Shocker - Korean Version |

음원 카탈로그를 변경할 때는 루트 `playlist.json`과 `seed.sql`의 ID·제목·R2 주소를 함께 맞춰야 합니다. 게임의 기존 숫자 인덱스와 연결된 순서는 유지합니다.

## 연결과 원격 적용

Supabase CLI 인증 또는 연결된 Supabase 도구에 해당 프로젝트 접근 권한이 있어야 합니다. 대시보드 URL은 대상 식별자이며 인증 수단은 아닙니다. 실제 비밀 값은 `.env`나 OS·CI 비밀 저장소에 보관하고, 브라우저나 Unity 배포 폴더에 넣지 않습니다. `.env.example`에는 빈 자리와 공개 URL만 들어 있습니다.

원격에는 다음 네 마이그레이션과 `seed.sql`을 적용했습니다. 같은 SQL을 직접 다시 실행하지 않습니다.

1. `20261110044457_fan_rooms.sql`: 테이블·접근 규칙·저장·발행·방문 RPC
2. `20261110044503_room_management.sql`: 프로필·공연장·프리셋 관리 RPC
3. `20261110044747_room_access_indexes.sql`: 소유권 조회와 외래 키 인덱스
4. `20261110045001_publication_fk_index.sql`: 현재 발행본 외래 키 인덱스

아래는 CLI v3으로 연결·이력을 확인하거나 이후 마이그레이션을 배포할 때의 명령입니다. 현재 원격 DB를 구성하기 위해 재실행할 필요는 없습니다.

```powershell
supabase login
supabase link ziyosbcfdsbirhdsgxax
supabase migration list --remote=ziyosbcfdsbirhdsgxax
supabase db push --remote=ziyosbcfdsbirhdsgxax --dry-run
supabase db push --remote=ziyosbcfdsbirhdsgxax
supabase db query --remote=ziyosbcfdsbirhdsgxax --file supabase/seed.sql
```

관리 도구로 SQL을 실행하는 경우에도 `migrations` 파일을 먼저 적용하고 `seed.sql`을 적용합니다. CLI와 관리 도구를 혼용할 때는 같은 마이그레이션을 두 번 적용하지 않도록 이력을 일치시킵니다. 운영 프로젝트에는 `db reset`을 사용하지 않습니다.

인증 설정은 사이트 URL `https://jtsgrit0.github.io/simbar_63/`, 정확히 일치하는 배포 URL과 로컬 `8000` 포트 리디렉션, 이메일 확인, 최소 비밀번호 8자, 익명 계정 생성 비활성화를 기준으로 준비했습니다. 공개 공연장 조회는 로그인 없는 `anon` 역할로 가능하며, 익명 계정 생성과는 별개입니다. OAuth 제공자 자격 증명은 만들지 않았습니다.

```powershell
supabase config diff --remote=ziyosbcfdsbirhdsgxax
supabase config push --remote=ziyosbcfdsbirhdsgxax
```

실제 확인한 CLI `3.1` 도움말 기준으로 설정 푸시는 **파일에 선언한 속성만** 변경하고 생략한 속성은 유지합니다. `config diff`에서 선언한 값의 변경 내용을 확인한 뒤 적용합니다. 로컬 Postgres를 사용할 때는 원격과 같은 메이저 버전 `17`을 `db.major_version`에 지정합니다.

## 원격 검증 결과

`tests/remote_smoke.sql`은 실제 원격 PostgreSQL에서 두 사용자의 JWT 클레임·DB 역할을 사용해 **26개 검사**를 통과했습니다. 소유권 분리, 잘못된 배치 거부, 저장 충돌, 발행 스냅샷 유지, 링크 한정 공개, 방문 중복 제거와 관리 RPC를 확인했습니다. 전체 트랜잭션을 롤백했으며 테스트 Auth 사용자·공연장·방문 기록이 남지 않았음을 확인했습니다. GoTrue를 통한 실제 회원가입·로그인 테스트는 아닙니다.

공개 publishable key로 실제 HTTP API **8개 검사**도 통과했습니다. 곡 5개 조회, 공개 RPC, 비공개 테이블 및 공연장 생성의 비로그인 접근 거부(`401`, SQLSTATE `42501`), Auth 엔드포인트 접근을 확인했습니다. 데이터 생성 없이 다음 명령으로 다시 검사할 수 있습니다.

```powershell
python Tools/verify_supabase_api.py
```

HTTP 결과는 `Temp/SupabaseSetup/public-api-verification.json`에 저장됩니다. 공개 Auth 응답에는 사이트 URL·리디렉션 목록·최소 비밀번호 값이 없어, 해당 값이 적용됐다고 판단하지 않았습니다.

보안 점검에는 의도적으로 허용한 `SECURITY DEFINER` RPC 실행 경고가 남아 있습니다(`anon` 3개, `authenticated` 13개). 직접 테이블 쓰기를 막고 좁은 RPC만 허용하는 설계이며, 로그인 사용자 작업은 함수 안에서 사용자·소유권을 확인합니다. 함수 검색 경로를 고정하고 `PUBLIC` 실행 권한을 철회한 뒤 역할별로 필요한 함수만 부여했습니다. 이는 공식 점검 문서의 의도적 RPC 사례([0028](https://supabase.com/docs/guides/observability/advisors?lint=0028_anon_security_definer_function_executable), [0029](https://supabase.com/docs/guides/observability/advisors?lint=0029_authenticated_security_definer_function_executable), Option 3)에 해당합니다. 보안 경고를 모두 제거했다고 보고하지 않습니다.

마지막 점검에서 누락된 외래 키 인덱스는 0개, 상태 경고는 0개입니다. 성능 점검의 미사용 인덱스 정보 12개는 새 DB에서 아직 운영 쿼리가 쌓이지 않은 상태이므로 유지했습니다.

## 로컬 DB 검증

`Tools/test_supabase_database.mjs`는 PGlite의 실제 PostgreSQL 엔진에 위 네 마이그레이션과 초기 데이터를 적용하고, 두 사용자와 비로그인 역할로 **88개 검사**를 통과했습니다. Supabase Auth/JWT는 테스트용으로 대체하므로 실제 로그인, 원격 Auth 설정, 여러 DB 연결의 동시 실행은 별도 검증 대상입니다.

Node.js LTS와 npm이 설치된 환경에서는 의존성을 무시되는 임시 폴더에 설치해 재현할 수 있습니다.

```powershell
npm install --prefix Temp/SupabaseDatabaseTests --no-save --package-lock=false @electric-sql/pglite@0.5.16
$env:SIMBAR_PGLITE_MODULE = (Resolve-Path 'Temp/SupabaseDatabaseTests/node_modules/@electric-sql/pglite/dist/index.js').Path
node Tools/test_supabase_database.mjs
```

결과는 `Temp/SupabaseDatabaseTests/results.json`에 저장됩니다. 템플릿의 구역·크기를 바꿀 때는 `version`을 올립니다. 승인된 아이템 아트만 `media_assets`에 등록합니다.

공식 참고: [CLI 설정](https://supabase.com/docs/guides/local-development/cli/config), [마이그레이션 명령](https://supabase.com/docs/reference/cli/supabase-db-push), [DB 함수](https://supabase.com/docs/guides/database/functions), [RLS](https://supabase.com/docs/guides/database/postgres/row-level-security), [인증 리디렉션](https://supabase.com/docs/guides/auth/redirect-urls).
