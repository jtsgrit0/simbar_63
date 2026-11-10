-- Owner-managed names and deletion without exposing direct table writes.
begin;

-- Historical publications retain their frozen snapshot when a preset is removed.
alter table public.room_publications alter column layout_id drop not null;
alter table public.room_publications drop constraint room_publications_layout_id_fkey;
alter table public.room_publications add constraint room_publications_layout_id_fkey
  foreign key (layout_id) references public.room_layouts(id) on delete set null;

create function public.update_profile(p_display_name text) returns void
language plpgsql security definer set search_path = '' as $$
declare v_user uuid := auth.uid();
begin
  if v_user is null then raise exception 'Sign in to update a profile' using errcode = '42501'; end if;
  if p_display_name is null or char_length(trim(p_display_name)) not between 1 and 40 then
    raise exception 'Display name must contain 1 to 40 characters' using errcode = '22023'; end if;
  update public.profiles set display_name = trim(p_display_name) where id = v_user;
  if not found then raise exception 'Profile was not found' using errcode = 'P0002'; end if;
end;
$$;

create function public.rename_room(p_room_id uuid, p_title text) returns void
language plpgsql security definer set search_path = '' as $$
begin
  if auth.uid() is null then raise exception 'Sign in to rename a room' using errcode = '42501'; end if;
  perform 1 from public.rooms where id = p_room_id and owner_id = auth.uid() for update;
  if not found then raise exception 'Room is not owned by this user' using errcode = '42501'; end if;
  if p_title is null or char_length(trim(p_title)) not between 1 and 80 then
    raise exception 'Room title must contain 1 to 80 characters' using errcode = '22023'; end if;
  update public.rooms set title = trim(p_title), updated_at = now() where id = p_room_id;
end;
$$;

create function public.rename_layout(p_layout_id uuid, p_name text) returns void
language plpgsql security definer set search_path = '' as $$
declare v_room uuid;
begin
  if auth.uid() is null then raise exception 'Sign in to rename a layout' using errcode = '42501'; end if;
  select r.id into v_room from public.rooms r join public.room_layouts l on l.room_id = r.id
  where l.id = p_layout_id and r.owner_id = auth.uid() for update of r;
  if not found then raise exception 'Layout is not owned by this user' using errcode = '42501'; end if;
  if p_name is null or char_length(trim(p_name)) not between 1 and 60 then
    raise exception 'Layout name must contain 1 to 60 characters' using errcode = '22023'; end if;
  update public.room_layouts set name = trim(p_name), updated_at = now() where id = p_layout_id;
  update public.rooms set updated_at = now() where id = v_room;
end;
$$;

create function public.delete_room(p_room_id uuid) returns void
language plpgsql security definer set search_path = '' as $$
begin
  if auth.uid() is null then raise exception 'Sign in to delete a room' using errcode = '42501'; end if;
  perform 1 from public.rooms where id = p_room_id and owner_id = auth.uid() for update;
  if not found then raise exception 'Room is not owned by this user' using errcode = '42501'; end if;
  delete from public.rooms where id = p_room_id;
end;
$$;

create function public.delete_layout(p_layout_id uuid) returns void
language plpgsql security definer set search_path = '' as $$
declare v_room uuid;
begin
  if auth.uid() is null then raise exception 'Sign in to delete a layout' using errcode = '42501'; end if;
  select r.id into v_room from public.rooms r join public.room_layouts l on l.room_id = r.id
  where l.id = p_layout_id and r.owner_id = auth.uid() for update of r;
  if not found then raise exception 'Layout is not owned by this user' using errcode = '42501'; end if;
  if exists (
    select 1 from public.rooms r join public.room_publications p on p.id = r.current_publication_id
    where r.id = v_room and p.layout_id = p_layout_id
  ) then raise exception 'Unpublish this layout before deleting it' using errcode = '22023'; end if;
  if (select count(*) from public.room_layouts where room_id = v_room) <= 1 then
    raise exception 'A room must retain at least one layout' using errcode = '22023'; end if;
  delete from public.room_layouts where id = p_layout_id;
  update public.rooms set updated_at = now() where id = v_room;
end;
$$;

revoke all on function public.update_profile(text), public.rename_room(uuid,text),
  public.rename_layout(uuid,text), public.delete_room(uuid), public.delete_layout(uuid)
  from public, anon, authenticated;
grant execute on function public.update_profile(text), public.rename_room(uuid,text),
  public.rename_layout(uuid,text), public.delete_room(uuid), public.delete_layout(uuid)
  to authenticated;

notify pgrst, 'reload schema';
commit;
