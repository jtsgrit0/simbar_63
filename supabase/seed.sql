-- Administrative catalogue data only; no users, rooms, visits, or credentials.
-- Audio URLs and stable track IDs mirror the root playlist.json (2026-11-10).
-- Safe to run again: existing catalogue rows are updated by stable IDs.
begin;

insert into public.bands (id, name, bio, active) values
  ('freewill', '프리윌(freewill)', '', true),
  ('julywantsmore', 'Julywantsmore', '', true)
on conflict (id) do update set name = excluded.name, active = excluded.active;

insert into public.media_assets (id, kind, provider, object_key, public_url, metadata) values
  ('audio_track_001', 'audio', 'r2', 'Electric Shocker.mp3',
    'https://pub-f61dee98441a42719d944abea5e0e1d2.r2.dev/Electric%20Shocker.mp3', '{"track_id":"track_001","content_type":"audio/mpeg"}'::jsonb),
  ('audio_track_002', 'audio', 'r2', 'Hey_spotdown.mp3',
    'https://pub-f61dee98441a42719d944abea5e0e1d2.r2.dev/Hey_spotdown.mp3', '{"track_id":"track_002","content_type":"audio/mpeg"}'::jsonb),
  ('audio_track_003', 'audio', 'r2', 'No Matter How.mp3',
    'https://pub-f61dee98441a42719d944abea5e0e1d2.r2.dev/No%20Matter%20How.mp3', '{"track_id":"track_003","content_type":"audio/mpeg"}'::jsonb),
  ('audio_track_004', 'audio', 'r2', 'Bedroom Manifesto.mp3',
    'https://pub-f61dee98441a42719d944abea5e0e1d2.r2.dev/Bedroom%20Manifesto.mp3', '{"track_id":"track_004","content_type":"audio/mpeg"}'::jsonb),
  ('audio_track_005', 'audio', 'r2', 'Electric Shocker - Korean Version.mp3',
    'https://pub-f61dee98441a42719d944abea5e0e1d2.r2.dev/Electric%20Shocker%20-%20Korean%20Version.mp3', '{"track_id":"track_005","content_type":"audio/mpeg"}'::jsonb)
on conflict (id) do update set
  kind = excluded.kind, provider = excluded.provider, object_key = excluded.object_key,
  public_url = excluded.public_url, metadata = excluded.metadata;

insert into public.tracks (id, band_id, title, audio_asset_id, duration_seconds, sort_order, active) values
  ('track_001', 'freewill', 'Electric Shocker', 'audio_track_001', 149.407347, 0, true),
  ('track_002', 'julywantsmore', 'Hey', 'audio_track_002', 192.470204, 1, true),
  ('track_003', 'julywantsmore', 'No Matter How', 'audio_track_003', 174.020499, 2, true),
  ('track_004', 'freewill', 'Bedroom Manifesto', 'audio_track_004', 161.295964, 3, true),
  ('track_005', 'freewill', 'Electric Shocker - Korean Version', 'audio_track_005', 149.407347, 4, true)
on conflict (id) do update set
  band_id = excluded.band_id, title = excluded.title, audio_asset_id = excluded.audio_asset_id,
  duration_seconds = excluded.duration_seconds, sort_order = excluded.sort_order, active = excluded.active;

insert into public.room_templates (id, name, version, definition, active) values
  ('starter_club', '작은 공연장', 1,
    '{"bounds":{"min_x":0,"max_x":100,"min_y":0,"max_y":100},"zones":["stage","wall","floor"],"max_placements":64}'::jsonb,
    true)
on conflict (id) do update set
  name = excluded.name, version = excluded.version, definition = excluded.definition, active = excluded.active;

-- Asset IDs are intentionally null until real, approved decoration art exists.
insert into public.items (id, band_id, name, category, asset_id, allowed_zones, is_free, active) values
  ('poster_freewill', 'freewill', '프리윌 포스터', 'poster', null, array['wall'], true, true),
  ('poster_julywantsmore', 'julywantsmore', '줄라이원츠모어 포스터', 'poster', null, array['wall'], true, true),
  ('starter_sofa', null, '공연장 소파', 'furniture', null, array['floor'], true, true),
  ('starter_lamp', null, '무대 조명', 'lighting', null, array['stage','floor'], true, true)
on conflict (id) do update set
  band_id = excluded.band_id, name = excluded.name, category = excluded.category,
  allowed_zones = excluded.allowed_zones, is_free = excluded.is_free, active = excluded.active;

commit;
