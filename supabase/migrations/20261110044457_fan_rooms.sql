-- Fan-created venues: private drafts, immutable published snapshots, R2 metadata.
-- Apply only after checking the target project's existing public schema.
begin;

create schema if not exists simbar_private;
revoke all on schema simbar_private from public, anon, authenticated;

create table public.profiles (
  id uuid primary key references auth.users(id) on delete cascade,
  display_name text not null default '음악 팬' check (char_length(display_name) between 1 and 40),
  created_at timestamptz not null default now()
);
create table public.bands (
  id text primary key,
  name text not null check (char_length(name) between 1 and 100),
  bio text not null default '',
  active boolean not null default true
);
create table public.media_assets (
  id text primary key,
  kind text not null check (kind in ('audio', 'image', 'model')),
  provider text not null check (provider in ('r2', 'supabase', 'external')),
  object_key text not null,
  public_url text not null check (public_url like 'https://%'),
  metadata jsonb not null default '{}' check (jsonb_typeof(metadata) = 'object'),
  active boolean not null default true
);
create table public.tracks (
  id text primary key,
  band_id text not null references public.bands(id),
  title text not null check (char_length(title) between 1 and 200),
  audio_asset_id text not null references public.media_assets(id),
  duration_seconds numeric check (duration_seconds > 0 and duration_seconds < 86400),
  sort_order integer not null check (sort_order >= 0),
  active boolean not null default true
);
create table public.room_templates (
  id text primary key,
  name text not null,
  version integer not null default 1 check (version > 0),
  definition jsonb not null check (jsonb_typeof(definition) = 'object'),
  active boolean not null default true
);
create table public.items (
  id text primary key,
  band_id text references public.bands(id),
  name text not null,
  category text not null,
  asset_id text references public.media_assets(id),
  allowed_zones text[] not null check (cardinality(allowed_zones) > 0),
  is_free boolean not null default false,
  active boolean not null default true
);
create table public.user_inventory (
  user_id uuid not null references public.profiles(id) on delete cascade,
  item_id text not null references public.items(id),
  acquired_at timestamptz not null default now(),
  source text not null default 'admin',
  primary key (user_id, item_id)
);
create table public.rooms (
  id uuid primary key default gen_random_uuid(),
  owner_id uuid not null references public.profiles(id) on delete cascade,
  template_id text not null references public.room_templates(id),
  band_id text references public.bands(id),
  title text not null check (char_length(title) between 1 and 80),
  current_publication_id uuid,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);
create index rooms_owner_idx on public.rooms(owner_id);
create table public.room_layouts (
  id uuid primary key default gen_random_uuid(),
  room_id uuid not null references public.rooms(id) on delete cascade,
  name text not null default '기본 배치' check (char_length(name) between 1 and 60),
  revision bigint not null default 0 check (revision >= 0),
  settings jsonb not null default '{}' check (jsonb_typeof(settings) = 'object'),
  updated_at timestamptz not null default now()
);
create index room_layouts_room_idx on public.room_layouts(room_id);
create table public.room_placements (
  layout_id uuid not null references public.room_layouts(id) on delete cascade,
  placement_key text not null check (char_length(placement_key) between 1 and 64),
  item_id text not null references public.items(id),
  zone text not null,
  x numeric not null,
  y numeric not null,
  rotation numeric not null default 0 check (rotation >= 0 and rotation < 360),
  scale numeric not null default 1 check (scale between 0.25 and 4),
  options jsonb not null default '{}' check (jsonb_typeof(options) = 'object'),
  primary key (layout_id, placement_key)
);
create index room_placements_item_idx on public.room_placements(item_id);
create table public.layout_tracks (
  layout_id uuid not null references public.room_layouts(id) on delete cascade,
  track_id text not null references public.tracks(id),
  position integer not null check (position between 0 and 49),
  primary key (layout_id, position),
  unique (layout_id, track_id)
);
create index layout_tracks_track_idx on public.layout_tracks(track_id);
create table public.room_publications (
  id uuid primary key default gen_random_uuid(),
  room_id uuid not null references public.rooms(id) on delete cascade,
  layout_id uuid not null references public.room_layouts(id),
  layout_revision bigint not null,
  slug text not null unique,
  visibility text not null check (visibility in ('public', 'unlisted')),
  snapshot jsonb not null check (jsonb_typeof(snapshot) = 'object'),
  published_at timestamptz not null default now(),
  unique (room_id, id)
);
create index room_publications_layout_idx on public.room_publications(layout_id);
-- The composite FK prevents a room from referencing another room's publication.
alter table public.rooms add constraint rooms_current_publication_fk
  foreign key (id, current_publication_id)
  references public.room_publications(room_id, id) deferrable initially deferred;
create table public.room_visits (
  publication_id uuid not null references public.room_publications(id) on delete cascade,
  session_id uuid not null,
  visitor_id uuid references auth.users(id) on delete set null,
  visited_at timestamptz not null default now(),
  primary key (publication_id, session_id)
);
create index room_visits_visitor_idx on public.room_visits(visitor_id);

create function simbar_private.handle_new_user() returns trigger
language plpgsql security definer set search_path = '' as $$
begin
  insert into public.profiles (id, display_name)
  values (new.id, coalesce(nullif(left(trim(new.raw_user_meta_data ->> 'display_name'), 40), ''), '음악 팬'))
  on conflict (id) do nothing;
  return new;
end;
$$;
create trigger simbar_profile_on_signup after insert on auth.users
  for each row execute function simbar_private.handle_new_user();
insert into public.profiles (id)
select id from auth.users on conflict (id) do nothing;

-- No broad client writes: RPCs own revision checks, inventory checks and snapshots.
alter table public.profiles enable row level security;
alter table public.bands enable row level security;
alter table public.media_assets enable row level security;
alter table public.tracks enable row level security;
alter table public.room_templates enable row level security;
alter table public.items enable row level security;
alter table public.user_inventory enable row level security;
alter table public.rooms enable row level security;
alter table public.room_layouts enable row level security;
alter table public.room_placements enable row level security;
alter table public.layout_tracks enable row level security;
alter table public.room_publications enable row level security;
alter table public.room_visits enable row level security;

create policy profiles_self_read on public.profiles for select to authenticated using (id = (select auth.uid()));
create policy bands_catalog_read on public.bands for select to anon, authenticated using (active);
create policy media_catalog_read on public.media_assets for select to anon, authenticated using (active);
create policy tracks_catalog_read on public.tracks for select to anon, authenticated using (active);
create policy templates_catalog_read on public.room_templates for select to anon, authenticated using (active);
create policy items_catalog_read on public.items for select to anon, authenticated using (active);
create policy inventory_self_read on public.user_inventory for select to authenticated using (user_id = (select auth.uid()));
create policy rooms_owner_read on public.rooms for select to authenticated using (owner_id = (select auth.uid()));
create policy layouts_owner_read on public.room_layouts for select to authenticated using (
  exists (select 1 from public.rooms r where r.id = room_id and r.owner_id = (select auth.uid()))
);
create policy placements_owner_read on public.room_placements for select to authenticated using (
  exists (select 1 from public.room_layouts l join public.rooms r on r.id = l.room_id
    where l.id = layout_id and r.owner_id = (select auth.uid()))
);
create policy layout_tracks_owner_read on public.layout_tracks for select to authenticated using (
  exists (select 1 from public.room_layouts l join public.rooms r on r.id = l.room_id
    where l.id = layout_id and r.owner_id = (select auth.uid()))
);
create policy publications_owner_read on public.room_publications for select to authenticated using (
  exists (select 1 from public.rooms r where r.id = room_id and r.owner_id = (select auth.uid()))
);
revoke all on public.profiles, public.bands, public.media_assets, public.tracks,
  public.room_templates, public.items, public.user_inventory, public.rooms,
  public.room_layouts, public.room_placements, public.layout_tracks,
  public.room_publications, public.room_visits from anon, authenticated;
grant select on public.bands, public.media_assets, public.tracks, public.room_templates, public.items to anon, authenticated;
grant select on public.profiles, public.user_inventory, public.rooms, public.room_layouts,
  public.room_placements, public.layout_tracks, public.room_publications to authenticated;

create function public.create_room(p_template_id text, p_title text, p_band_id text default null)
returns uuid language plpgsql security definer set search_path = '' as $$
declare v_user uuid := auth.uid(); v_room uuid;
begin
  if v_user is null then raise exception 'Sign in to create a room' using errcode = '42501'; end if;
  if p_title is null or char_length(trim(p_title)) not between 1 and 80 then
    raise exception 'Room title must contain 1 to 80 characters' using errcode = '22023'; end if;
  if not exists (select 1 from public.room_templates where id = p_template_id and active) then
    raise exception 'Unknown room template' using errcode = '22023'; end if;
  if p_band_id is not null and not exists (select 1 from public.bands where id = p_band_id and active) then
    raise exception 'Unknown band' using errcode = '22023'; end if;
  insert into public.profiles(id) values(v_user) on conflict (id) do nothing;
  insert into public.rooms(owner_id, template_id, band_id, title)
  values(v_user, p_template_id, p_band_id, trim(p_title)) returning id into v_room;
  insert into public.room_layouts(room_id) values(v_room);
  return v_room;
end;
$$;

create function public.create_layout(p_room_id uuid, p_name text default '새 배치')
returns uuid language plpgsql security definer set search_path = '' as $$
declare v_layout uuid;
begin
  perform 1 from public.rooms where id = p_room_id and owner_id = auth.uid() for update;
  if not found then raise exception 'Room is not owned by this user' using errcode = '42501'; end if;
  if p_name is null or char_length(trim(p_name)) not between 1 and 60 then
    raise exception 'Layout name must contain 1 to 60 characters' using errcode = '22023'; end if;
  if (select count(*) from public.room_layouts where room_id = p_room_id) >= 20 then
    raise exception 'A room supports up to 20 saved layouts' using errcode = '22023'; end if;
  insert into public.room_layouts(room_id, name) values(p_room_id, trim(p_name)) returning id into v_layout;
  return v_layout;
end;
$$;

create function simbar_private.validate_layout(p_layout_id uuid) returns void
language plpgsql security definer set search_path = '' as $$
declare v_room public.rooms; v_def jsonb; v_settings jsonb;
begin
  select r.* into v_room from public.rooms r join public.room_layouts l on l.room_id = r.id where l.id = p_layout_id;
  if not found then raise exception 'Unknown layout' using errcode = '22023'; end if;
  select definition into v_def from public.room_templates where id = v_room.template_id and active;
  if v_def is null then raise exception 'Template is unavailable' using errcode = '22023'; end if;
  select settings into v_settings from public.room_layouts where id = p_layout_id;
  if exists (select 1 from jsonb_object_keys(v_settings) k where k not in ('lighting','ambience'))
     or (v_settings ? 'lighting' and coalesce(v_settings->>'lighting','') not in ('warm','cool','neutral'))
     or (v_settings ? 'ambience' and coalesce(v_settings->>'ambience','') not in ('quiet','crowd')) then
    raise exception 'Unsupported layout settings' using errcode = '22023'; end if;
  if (select count(*) from public.room_placements where layout_id = p_layout_id) >
     least(coalesce((v_def->>'max_placements')::integer, 64), 128) then
    raise exception 'Too many placements' using errcode = '22023'; end if;
  if exists (
    select 1 from public.room_placements p join public.items i on i.id = p.item_id
    where p.layout_id = p_layout_id and (
      not i.active or (not i.is_free and not exists (
        select 1 from public.user_inventory u where u.user_id = v_room.owner_id and u.item_id = i.id))
      or (i.asset_id is not null and not exists (
        select 1 from public.media_assets a where a.id = i.asset_id and a.active and a.kind in ('image','model')))
      or not (p.zone = any(i.allowed_zones))
      or not coalesce((v_def->'zones') ? p.zone, false)
      or p.x::text in ('NaN','Infinity','-Infinity') or p.y::text in ('NaN','Infinity','-Infinity')
      or p.x < (v_def#>>'{bounds,min_x}')::numeric or p.x > (v_def#>>'{bounds,max_x}')::numeric
      or p.y < (v_def#>>'{bounds,min_y}')::numeric or p.y > (v_def#>>'{bounds,max_y}')::numeric
      or exists (select 1 from jsonb_object_keys(p.options) k where k <> 'flip_x')
      or (p.options ? 'flip_x' and jsonb_typeof(p.options->'flip_x') <> 'boolean')
    )
  ) then raise exception 'Invalid, unavailable or unowned placement' using errcode = '22023'; end if;
  if exists (
    select 1 from public.layout_tracks lt join public.tracks t on t.id = lt.track_id
    join public.media_assets m on m.id = t.audio_asset_id join public.bands b on b.id = t.band_id
    where lt.layout_id = p_layout_id and (not t.active or not m.active or m.kind <> 'audio' or not b.active)
  ) then raise exception 'Track is unavailable' using errcode = '22023'; end if;
end;
$$;

create function public.save_layout(p_layout_id uuid, p_expected_revision bigint,
  p_placements jsonb, p_tracks jsonb, p_settings jsonb default '{}')
returns bigint language plpgsql security definer set search_path = '' as $$
declare v_room uuid; v_revision bigint; v_entry jsonb; v_position integer := 0;
begin
  select r.id into v_room from public.rooms r join public.room_layouts l on l.room_id = r.id
  where l.id = p_layout_id and r.owner_id = auth.uid() for update of r;
  if not found then raise exception 'Layout is not owned by this user' using errcode = '42501'; end if;
  select revision into v_revision from public.room_layouts where id = p_layout_id;
  if p_expected_revision is null or p_expected_revision <> v_revision then
    raise exception 'Layout has changed; reload before saving' using errcode = '40001'; end if;
  if jsonb_typeof(p_placements) is distinct from 'array' or jsonb_typeof(p_tracks) is distinct from 'array'
     or jsonb_typeof(p_settings) is distinct from 'object' then
    raise exception 'Expected placement/track arrays and settings object' using errcode = '22023'; end if;
  if octet_length(p_placements::text) > 131072 or octet_length(p_settings::text) > 4096
     or octet_length(p_tracks::text) > 16384
     or jsonb_array_length(p_placements) > 128 or jsonb_array_length(p_tracks) > 50 then
    raise exception 'Layout payload is too large' using errcode = '22023'; end if;
  delete from public.room_placements where layout_id = p_layout_id;
  delete from public.layout_tracks where layout_id = p_layout_id;
  for v_entry in select value from jsonb_array_elements(p_placements) loop
    if jsonb_typeof(v_entry) <> 'object'
       or exists (select 1 from jsonb_object_keys(v_entry) k where k not in ('key','item_id','zone','x','y','rotation','scale','options'))
       or jsonb_typeof(v_entry->'key') is distinct from 'string'
       or jsonb_typeof(v_entry->'item_id') is distinct from 'string'
       or jsonb_typeof(v_entry->'zone') is distinct from 'string'
       or jsonb_typeof(v_entry->'x') is distinct from 'number'
       or jsonb_typeof(v_entry->'y') is distinct from 'number'
       or (v_entry ? 'rotation' and jsonb_typeof(v_entry->'rotation') <> 'number')
       or (v_entry ? 'scale' and jsonb_typeof(v_entry->'scale') <> 'number')
       or (v_entry ? 'options' and jsonb_typeof(v_entry->'options') <> 'object') then
      raise exception 'Invalid placement fields' using errcode = '22023'; end if;
    insert into public.room_placements(layout_id, placement_key, item_id, zone, x, y, rotation, scale, options)
    values(p_layout_id, v_entry->>'key', v_entry->>'item_id', v_entry->>'zone',
      (v_entry->>'x')::numeric, (v_entry->>'y')::numeric, coalesce((v_entry->>'rotation')::numeric,0),
      coalesce((v_entry->>'scale')::numeric,1), coalesce(v_entry->'options','{}'::jsonb));
  end loop;
  for v_entry in select value from jsonb_array_elements(p_tracks) loop
    if jsonb_typeof(v_entry) <> 'string' then raise exception 'Track IDs must be strings' using errcode = '22023'; end if;
    insert into public.layout_tracks(layout_id, track_id, position) values(p_layout_id, v_entry#>>'{}', v_position);
    v_position := v_position + 1;
  end loop;
  update public.room_layouts set settings = p_settings where id = p_layout_id;
  perform simbar_private.validate_layout(p_layout_id);
  update public.room_layouts set revision = revision + 1, updated_at = now() where id = p_layout_id returning revision into v_revision;
  update public.rooms set updated_at = now() where id = v_room;
  return v_revision;
end;
$$;

-- Layout selection is explicit: a room may contain several band/occasion presets.
create function public.publish_room(p_room_id uuid, p_expected_revision bigint,
  p_visibility text default 'public', p_layout_id uuid default null)
returns text language plpgsql security definer set search_path = '' as $$
declare v_room public.rooms; v_layout public.room_layouts; v_id uuid := gen_random_uuid();
  v_slug text := replace(gen_random_uuid()::text,'-',''); v_snapshot jsonb;
begin
  select * into v_room from public.rooms where id = p_room_id and owner_id = auth.uid() for update;
  if not found then raise exception 'Room is not owned by this user' using errcode = '42501'; end if;
  if p_visibility is null or p_visibility not in ('public','unlisted') then
    raise exception 'Invalid publication visibility' using errcode = '22023'; end if;
  if p_layout_id is null and (select count(*) from public.room_layouts where room_id = p_room_id) <> 1 then
    raise exception 'Choose a layout when the room has multiple presets' using errcode = '22023'; end if;
  select * into v_layout from public.room_layouts where room_id = p_room_id
    and (p_layout_id is null or id = p_layout_id) order by id limit 1;
  if not found then raise exception 'Layout does not belong to this room' using errcode = '22023'; end if;
  if p_expected_revision is null or p_expected_revision <> v_layout.revision then
    raise exception 'Layout has changed; reload before publishing' using errcode = '40001'; end if;
  perform simbar_private.validate_layout(v_layout.id);
  select jsonb_build_object(
    'schema_version',1,
    'room',jsonb_build_object('title',v_room.title,'band_id',v_room.band_id),
    'creator',jsonb_build_object('display_name',(select display_name from public.profiles where id = v_room.owner_id)),
    'template',jsonb_build_object('id',t.id,'version',t.version,'definition',t.definition),
    'layout',jsonb_build_object('revision',v_layout.revision,'settings',v_layout.settings,
      'placements',coalesce((select jsonb_agg(jsonb_build_object(
        'key',p.placement_key,'item_id',p.item_id,
        'item',jsonb_build_object('name',i.name,'category',i.category,
          'asset',case when m.id is null then null else jsonb_build_object('kind',m.kind,'url',m.public_url) end),
        'zone',p.zone,'x',p.x,'y',p.y,
        'rotation',p.rotation,'scale',p.scale,'options',p.options) order by p.placement_key)
        from public.room_placements p join public.items i on i.id = p.item_id
        left join public.media_assets m on m.id = i.asset_id
        where p.layout_id = v_layout.id),'[]'::jsonb),
      'tracks',coalesce((select jsonb_agg(jsonb_build_object(
        'id',tr.id,'title',tr.title,'artist',b.name,'audio_url',m.public_url,'duration_seconds',tr.duration_seconds) order by lt.position)
        from public.layout_tracks lt join public.tracks tr on tr.id = lt.track_id
        join public.bands b on b.id = tr.band_id join public.media_assets m on m.id = tr.audio_asset_id
        where lt.layout_id = v_layout.id),'[]'::jsonb))) into v_snapshot
  from public.room_templates t where t.id = v_room.template_id;
  insert into public.room_publications(id,room_id,layout_id,layout_revision,slug,visibility,snapshot)
  values(v_id,p_room_id,v_layout.id,v_layout.revision,v_slug,p_visibility,v_snapshot);
  update public.rooms set current_publication_id = v_id, updated_at = now() where id = p_room_id;
  return v_slug;
end;
$$;

create function public.unpublish_room(p_room_id uuid) returns void
language plpgsql security definer set search_path = '' as $$
begin
  update public.rooms set current_publication_id = null, updated_at = now()
  where id = p_room_id and owner_id = auth.uid();
  if not found then raise exception 'Room is not owned by this user' using errcode = '42501'; end if;
end;
$$;

create function public.get_published_room(p_slug text) returns jsonb
language sql stable security definer set search_path = '' as $$
  select jsonb_build_object('id',p.id,'slug',p.slug,'visibility',p.visibility,
    'published_at',p.published_at,'snapshot',p.snapshot,
    'visit_count',(select count(*) from public.room_visits v where v.publication_id = p.id))
  from public.room_publications p join public.rooms r on r.current_publication_id = p.id
  where p.slug = p_slug;
$$;

create function public.list_public_rooms(p_limit integer default 20, p_offset integer default 0) returns jsonb
language sql stable security definer set search_path = '' as $$
  select coalesce(jsonb_agg(row_data order by published_at desc, id),'[]'::jsonb) from (
    select p.id, p.published_at, jsonb_build_object('slug',p.slug,'published_at',p.published_at,
      'room',p.snapshot->'room','creator',p.snapshot->'creator',
      'visit_count',(select count(*) from public.room_visits v where v.publication_id = p.id)) row_data
    from public.room_publications p join public.rooms r on r.current_publication_id = p.id
    where p.visibility = 'public' order by p.published_at desc,p.id
    limit greatest(1,least(coalesce(p_limit,20),100)) offset greatest(0,coalesce(p_offset,0))
  ) published;
$$;

create function public.record_visit(p_slug text, p_session_id uuid) returns bigint
language plpgsql security definer set search_path = '' as $$
declare v_publication uuid; v_owner uuid;
begin
  if p_session_id is null then raise exception 'Session ID is required' using errcode = '22023'; end if;
  select p.id,r.owner_id into v_publication,v_owner from public.room_publications p
  join public.rooms r on r.current_publication_id = p.id where p.slug = p_slug for share of r;
  if not found then raise exception 'Published room was not found' using errcode = 'P0002'; end if;
  if auth.uid() is distinct from v_owner then
    insert into public.room_visits(publication_id,session_id,visitor_id)
    values(v_publication,p_session_id,auth.uid()) on conflict (publication_id,session_id) do nothing;
  end if;
  return (select count(*) from public.room_visits where publication_id = v_publication);
end;
$$;

revoke all on function simbar_private.handle_new_user(), simbar_private.validate_layout(uuid) from public, anon, authenticated;
revoke all on function public.create_room(text,text,text), public.create_layout(uuid,text),
  public.save_layout(uuid,bigint,jsonb,jsonb,jsonb), public.publish_room(uuid,bigint,text,uuid),
  public.unpublish_room(uuid), public.get_published_room(text), public.list_public_rooms(integer,integer),
  public.record_visit(text,uuid) from public, anon, authenticated;
grant execute on function public.create_room(text,text,text), public.create_layout(uuid,text),
  public.save_layout(uuid,bigint,jsonb,jsonb,jsonb), public.publish_room(uuid,bigint,text,uuid),
  public.unpublish_room(uuid) to authenticated;
grant execute on function public.get_published_room(text), public.list_public_rooms(integer,integer),
  public.record_visit(text,uuid) to anon, authenticated;

comment on table public.room_publications is 'Server-built immutable snapshots. Only the current pointer is shared; drafts stay private.';
comment on table public.room_visits is 'Unique publication/session pairs; not verified unique people. No client access to raw visitor rows.';
comment on table public.media_assets is 'Asset metadata only. Existing MP3 objects remain on Cloudflare R2.';
notify pgrst, 'reload schema';
commit;
