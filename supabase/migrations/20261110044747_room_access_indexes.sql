begin;

-- Catalogue joins, ownership lookups, and FK maintenance as rooms accumulate.
create index items_asset_idx on public.items(asset_id);
create index items_band_idx on public.items(band_id);
create index rooms_band_idx on public.rooms(band_id);
create index rooms_template_idx on public.rooms(template_id);
create index rooms_current_publication_idx on public.rooms(current_publication_id, id);
create index tracks_audio_asset_idx on public.tracks(audio_asset_id);
create index tracks_band_idx on public.tracks(band_id);
create index user_inventory_item_idx on public.user_inventory(item_id);

-- Raw visit rows are never a client API. Aggregate access stays in the RPCs.
-- SELECT privileges remain revoked; this policy also makes the intent explicit.
create policy visits_no_client_read on public.room_visits for select
  to anon, authenticated using (false);

commit;
