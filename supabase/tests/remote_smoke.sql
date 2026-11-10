-- Run as the project database administrator after migrations and seed.sql.
-- Exercises real database roles/JWT claims; this does not test GoTrue login.
-- Both generated Auth fixtures and every dependent row are rolled back.
-- Direct auth.users inserts do not invoke an email delivery API.
begin;

do $smoke$
declare
  v_admin text := current_user;
  v_alice uuid := gen_random_uuid();
  v_bob uuid := gen_random_uuid();
  v_session uuid := gen_random_uuid();
  v_other_session uuid := gen_random_uuid();
  v_room uuid;
  v_layout uuid;
  v_revision bigint;
  v_slug text;
  v_unlisted_slug text;
  v_snapshot jsonb;
  v_result jsonb;
  v_denied boolean;
  v_checks integer := 0;
  v_placements jsonb := '[{"key":"smoke-poster","item_id":"poster_freewill","zone":"wall","x":15,"y":25,"rotation":0,"scale":1,"options":{}}]'::jsonb;
begin
  insert into auth.users(id,email,raw_user_meta_data,raw_app_meta_data,aud,role)
  values
    (v_alice,'simbar-db-smoke-' || v_alice::text || '@example.invalid',
      '{"display_name":"Smoke Alice"}','{}','authenticated','authenticated'),
    (v_bob,'simbar-db-smoke-' || v_bob::text || '@example.invalid',
      '{"display_name":"Smoke Bob"}','{}','authenticated','authenticated');
  if (select count(*) from public.profiles where id in (v_alice,v_bob)) <> 2 then
    raise exception 'Smoke: Auth trigger did not create both profiles';
  end if;
  v_checks := v_checks + 1;

  perform set_config('request.jwt.claim.sub',v_alice::text,true);
  perform set_config('request.jwt.claims',jsonb_build_object('sub',v_alice,'role','authenticated')::text,true);
  execute 'set local role authenticated';
  if auth.uid() is distinct from v_alice then raise exception 'Smoke: Alice JWT claim failed'; end if;
  v_checks := v_checks + 1;

  v_room := public.create_room('starter_club','Transactional smoke club','freewill');
  select id into strict v_layout from public.room_layouts where room_id = v_room;
  if (select count(*) from public.rooms where id = v_room and owner_id = v_alice) <> 1 then
    raise exception 'Smoke: owner cannot read own room';
  end if;
  v_checks := v_checks + 1;

  v_revision := public.save_layout(v_layout,0,v_placements,'["track_001","track_004"]','{"lighting":"warm"}');
  if v_revision <> 1 then raise exception 'Smoke: first save revision failed'; end if;
  v_checks := v_checks + 1;

  v_denied := false;
  begin
    update public.rooms set title = 'Forbidden direct write' where id = v_room;
  exception when insufficient_privilege then v_denied := true;
  end;
  if not v_denied then raise exception 'Smoke: direct client write was allowed'; end if;
  v_checks := v_checks + 1;

  v_denied := false;
  begin
    perform public.save_layout(v_layout,0,'[]','[]','{}');
  exception when serialization_failure then v_denied := true;
  end;
  if not v_denied then raise exception 'Smoke: stale save was allowed'; end if;
  v_checks := v_checks + 1;

  v_slug := public.publish_room(v_room,1,'public',v_layout);
  v_snapshot := public.get_published_room(v_slug)->'snapshot';
  if v_snapshot is null or v_snapshot#>>'{room,title}' <> 'Transactional smoke club'
     or v_snapshot#>>'{creator,display_name}' <> 'Smoke Alice'
     or jsonb_array_length(v_snapshot#>'{layout,tracks}') <> 2 then
    raise exception 'Smoke: published snapshot content failed';
  end if;
  v_checks := v_checks + 1;

  execute format('set local role %I',v_admin);
  perform set_config('request.jwt.claim.sub',v_bob::text,true);
  perform set_config('request.jwt.claims',jsonb_build_object('sub',v_bob,'role','authenticated')::text,true);
  execute 'set local role authenticated';
  if auth.uid() is distinct from v_bob then raise exception 'Smoke: Bob JWT claim failed'; end if;
  v_checks := v_checks + 1;

  if exists (select 1 from public.rooms where id = v_room)
     or exists (select 1 from public.room_layouts where id = v_layout)
     or exists (select 1 from public.room_placements where layout_id = v_layout) then
    raise exception 'Smoke: another account can read private drafts';
  end if;
  v_checks := v_checks + 1;

  v_denied := false;
  begin
    perform public.save_layout(v_layout,1,'[]','[]','{}');
  exception when insufficient_privilege then v_denied := true;
  end;
  if not v_denied then raise exception 'Smoke: another account can save owner draft'; end if;
  v_checks := v_checks + 1;

  v_denied := false;
  begin
    perform public.publish_room(v_room,1,'public',v_layout);
  exception when insufficient_privilege then v_denied := true;
  end;
  if not v_denied then raise exception 'Smoke: another account can publish owner room'; end if;
  v_checks := v_checks + 1;

  v_denied := false;
  begin
    perform public.unpublish_room(v_room);
  exception when insufficient_privilege then v_denied := true;
  end;
  if not v_denied then raise exception 'Smoke: another account can unpublish owner room'; end if;
  v_checks := v_checks + 1;

  execute format('set local role %I',v_admin);
  perform set_config('request.jwt.claim.sub','',true);
  perform set_config('request.jwt.claims','{"role":"anon"}',true);
  execute 'set local role anon';
  if (select count(*) from public.tracks where id = any(array[
    'track_001','track_002','track_003','track_004','track_005'])) <> 5 then
    raise exception 'Smoke: anonymous catalog read failed';
  end if;
  v_checks := v_checks + 1;

  v_denied := false;
  begin
    perform 1 from public.room_publications limit 1;
  exception when insufficient_privilege then v_denied := true;
  end;
  if not v_denied then raise exception 'Smoke: anonymous publication enumeration was allowed'; end if;
  v_checks := v_checks + 1;

  v_denied := false;
  begin
    perform 1 from public.room_visits limit 1;
  exception when insufficient_privilege then v_denied := true;
  end;
  if not v_denied then raise exception 'Smoke: anonymous raw visit read was allowed'; end if;
  v_checks := v_checks + 1;

  v_result := public.get_published_room(v_slug);
  if v_result->'snapshot' is distinct from v_snapshot then
    raise exception 'Smoke: anonymous published room lookup failed';
  end if;
  v_checks := v_checks + 1;

  if public.record_visit(v_slug,v_session) <> 1 then raise exception 'Smoke: first visit count failed'; end if;
  v_checks := v_checks + 1;
  if public.record_visit(v_slug,v_session) <> 1 then raise exception 'Smoke: duplicate visit counted twice'; end if;
  v_checks := v_checks + 1;

  execute format('set local role %I',v_admin);
  perform set_config('request.jwt.claim.sub',v_bob::text,true);
  perform set_config('request.jwt.claims',jsonb_build_object('sub',v_bob,'role','authenticated')::text,true);
  execute 'set local role authenticated';
  if public.record_visit(v_slug,v_other_session) <> 2 then raise exception 'Smoke: authenticated visit count failed'; end if;
  v_checks := v_checks + 1;

  execute format('set local role %I',v_admin);
  perform set_config('request.jwt.claim.sub',v_alice::text,true);
  perform set_config('request.jwt.claims',jsonb_build_object('sub',v_alice,'role','authenticated')::text,true);
  execute 'set local role authenticated';
  if public.record_visit(v_slug,gen_random_uuid()) <> 2 then raise exception 'Smoke: owner inflated visit count'; end if;
  v_checks := v_checks + 1;

  v_revision := public.save_layout(v_layout,1,'[]','["track_002"]','{"lighting":"cool"}');
  if v_revision <> 2 then raise exception 'Smoke: next save revision failed'; end if;
  v_checks := v_checks + 1;
  if public.get_published_room(v_slug)->'snapshot' is distinct from v_snapshot then
    raise exception 'Smoke: draft changes mutated published snapshot';
  end if;
  v_checks := v_checks + 1;

  v_unlisted_slug := public.publish_room(v_room,2,'unlisted',v_layout);
  if public.get_published_room(v_unlisted_slug) is null or exists (
    select 1 from jsonb_array_elements(public.list_public_rooms(100,0)) r where r->>'slug' = v_unlisted_slug
  ) then raise exception 'Smoke: unlisted lookup or directory privacy failed'; end if;
  v_checks := v_checks + 1;
  if public.get_published_room(v_slug) is not null then raise exception 'Smoke: superseded link is still shared'; end if;
  v_checks := v_checks + 1;

  perform public.unpublish_room(v_room);
  if public.get_published_room(v_unlisted_slug) is not null then raise exception 'Smoke: unpublished link is still shared'; end if;
  v_checks := v_checks + 1;
  v_denied := false;
  begin
    perform public.record_visit(v_unlisted_slug,gen_random_uuid());
  exception when no_data_found then v_denied := true;
  end;
  if not v_denied then raise exception 'Smoke: unpublished room accepted a visit'; end if;
  v_checks := v_checks + 1;

  execute format('set local role %I',v_admin);
  if v_checks <> 26 then raise exception 'Smoke: unexpected check count %',v_checks; end if;
  raise notice 'Supabase remote smoke: % checks passed; transaction will be rolled back',v_checks;
end;
$smoke$;

rollback;
select '26 database checks passed; all temporary Auth users, rooms, snapshots and visits rolled back' as result;
