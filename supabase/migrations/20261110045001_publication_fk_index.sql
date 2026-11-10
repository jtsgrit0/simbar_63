-- FK maintenance starts with the room ID; public lookup starts with publication ID.
-- Keep a covering index for each access order.
create index rooms_publication_fk_idx on public.rooms(id, current_publication_id);
