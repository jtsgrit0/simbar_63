-- Public totals only. The anonymous playback ledger stays outside the Data API.
-- R2 audio and the existing track/asset records are unchanged.
begin;

create schema if not exists simbar_private;
revoke all on schema simbar_private from public, anon, authenticated;

create table public.track_play_counts (
  track_id text primary key references public.tracks(id),
  play_count bigint not null default 0 check (play_count >= 0),
  updated_at timestamptz not null default now()
);

create table simbar_private.music_play_events (
  play_id uuid primary key,
  track_id text not null references public.tracks(id),
  source text not null check (source in ('web', 'windows')),
  heard_seconds numeric(10, 3) not null check (heard_seconds between 30 and 86400),
  ticket_issued_at timestamptz not null,
  counted_at timestamptz not null default now()
);
create index music_play_events_track_id_idx
  on simbar_private.music_play_events(track_id);

alter table public.track_play_counts enable row level security;
alter table simbar_private.music_play_events enable row level security;

revoke all on public.track_play_counts from public, anon, authenticated;
revoke all on simbar_private.music_play_events from public, anon, authenticated;
grant select on public.track_play_counts to anon, authenticated;
grant usage on schema simbar_private to service_role;
grant select, insert on simbar_private.music_play_events to service_role;
grant select, insert, update on public.track_play_counts to service_role;
grant select on public.tracks, public.bands, public.media_assets to service_role;

create policy track_play_counts_public_read on public.track_play_counts
  for select to anon, authenticated
  using (exists (
    select 1 from public.tracks t
    where t.id = track_play_counts.track_id and t.active
  ));

insert into public.track_play_counts(track_id)
select id from public.tracks
where id in ('track_001', 'track_002', 'track_003', 'track_004', 'track_005');

-- Only the Edge Function's server credential may call this invoker function.
-- A unique play UUID and the conditional INSERT make retries/concurrency safe.
create function public.record_music_play(
  p_play_id uuid,
  p_track_id text,
  p_source text,
  p_heard_seconds numeric,
  p_ticket_issued_at timestamptz
)
returns table(track_id text, play_count text, counted boolean)
language plpgsql
security invoker
set search_path = ''
as $$
declare
  v_inserted uuid;
  v_existing_track text;
  v_count bigint;
  v_now timestamptz := clock_timestamp();
begin
  if p_play_id is null
     or p_track_id is null
     or p_track_id not in ('track_001', 'track_002', 'track_003', 'track_004', 'track_005')
     or p_source is null or p_source not in ('web', 'windows')
     or p_heard_seconds is null or not (p_heard_seconds between 30 and 86400)
     or p_ticket_issued_at is null
     or p_ticket_issued_at > v_now - interval '30 seconds'
     or p_ticket_issued_at < v_now - interval '24 hours' then
    raise exception using errcode = '22023', message = 'invalid qualified music play';
  end if;

  if not exists (
    select 1
    from public.tracks t
    join public.bands b on b.id = t.band_id
    join public.media_assets a on a.id = t.audio_asset_id
    where t.id = p_track_id and t.active and b.active and a.active
      and a.kind = 'audio' and a.provider = 'r2'
  ) then
    raise exception using errcode = '22023', message = 'inactive or unknown track';
  end if;

  insert into simbar_private.music_play_events(
    play_id, track_id, source, heard_seconds, ticket_issued_at, counted_at
  ) values (
    p_play_id, p_track_id, p_source, p_heard_seconds, p_ticket_issued_at, v_now
  )
  on conflict on constraint music_play_events_pkey do nothing
  returning play_id into v_inserted;

  if v_inserted is not null then
    insert into public.track_play_counts(track_id, play_count, updated_at)
    values (p_track_id, 1, v_now)
    on conflict on constraint track_play_counts_pkey do update
      set play_count = public.track_play_counts.play_count + 1,
          updated_at = excluded.updated_at
    returning public.track_play_counts.play_count into v_count;
  else
    select e.track_id into v_existing_track
    from simbar_private.music_play_events e where e.play_id = p_play_id;
    if v_existing_track is distinct from p_track_id then
      raise exception using errcode = '23505', message = 'play UUID belongs to another track';
    end if;
    select c.play_count into strict v_count
    from public.track_play_counts c where c.track_id = p_track_id;
  end if;

  return query select p_track_id, v_count::text, v_inserted is not null;
end;
$$;

revoke all on function public.record_music_play(uuid, text, text, numeric, timestamptz)
  from public, anon, authenticated;
grant execute on function public.record_music_play(uuid, text, text, numeric, timestamptz)
  to service_role;

comment on table public.track_play_counts is
  'Public aggregate of playback episodes with at least 30 actual heard seconds; no visitor records.';
comment on table simbar_private.music_play_events is
  'Private idempotency ledger. No user IDs, raw IP addresses, or browser identifiers are stored.';
comment on function public.record_music_play(uuid, text, text, numeric, timestamptz) is
  'Service-only atomic count; invoked after Edge HMAC ticket and 30-second qualification checks.';

commit;
