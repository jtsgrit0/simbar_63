// Executes the real migrations in disposable PostgreSQL WASM, with Supabase's
// auth roles mocked locally. No remote connection or project credentials used.
// Install @electric-sql/pglite, or set SIMBAR_PGLITE_MODULE to its index.js path.
import assert from 'node:assert/strict';
import {readFile, readdir, mkdir, writeFile} from 'node:fs/promises';
import {fileURLToPath, pathToFileURL} from 'node:url';
import {resolve, dirname} from 'node:path';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const ALICE = '11111111-1111-4111-8111-111111111111';
const BOB = '22222222-2222-4222-8222-222222222222';
const SESSION = '33333333-3333-4333-8333-333333333333';

export async function runSupabaseDatabaseTests({modulePath = process.env.SIMBAR_PGLITE_MODULE} = {}) {
  let engine;
  if (modulePath) engine = await import(pathToFileURL(resolve(modulePath)).href);
  else {
    try { engine = await import('@electric-sql/pglite'); }
    catch {
      engine = await import(pathToFileURL(resolve(ROOT,
        'Temp/SupabaseDatabaseTests/pglite/package/dist/index.js')).href);
    }
  }
  const db = new engine.PGlite();
  await db.waitReady;
  const checks = [];
  const check = (name, actual, expected) => {
    assert.deepEqual(actual, expected, name);
    checks.push(name);
  };
  const scalar = async (sql, params = []) => Object.values((await db.query(sql, params)).rows[0])[0];
  const asRole = async (role, user, action) => {
    assert.ok(['anon', 'authenticated', 'service_role'].includes(role));
    await db.exec('begin');
    try {
      await db.exec(`set local role ${role}`);
      await db.query("select set_config('request.jwt.claim.sub', $1, true)", [user ?? '']);
      await db.query("select set_config('request.jwt.claims', $1, true)",
        [JSON.stringify({sub: user, role})]);
      const value = await action();
      await db.exec('commit');
      return value;
    } catch (error) {
      await db.exec('rollback');
      throw error;
    }
  };
  const alice = action => asRole('authenticated', ALICE, action);
  const bob = action => asRole('authenticated', BOB, action);
  const anon = action => asRole('anon', null, action);
  const denied = async (name, action, code = '42501') => {
    await assert.rejects(action, error => error.code === code,
      `${name}: expected PostgreSQL SQLSTATE ${code}`);
    checks.push(name);
  };
  try {
    await db.exec(`
      create role anon nologin;
      create role authenticated nologin;
      create role service_role nologin bypassrls;
      create schema auth;
      create schema extensions;
      create table auth.users (
        id uuid primary key,
        email text,
        raw_user_meta_data jsonb not null default '{}',
        raw_app_meta_data jsonb not null default '{}',
        created_at timestamptz not null default now()
      );
      create function auth.uid() returns uuid language sql stable as
        $$ select nullif(current_setting('request.jwt.claim.sub', true), '')::uuid $$;
      create function auth.jwt() returns jsonb language sql stable as
        $$ select coalesce(nullif(current_setting('request.jwt.claims', true), ''), '{}')::jsonb $$;
      create function auth.role() returns text language sql stable as
        $$ select current_setting('request.jwt.claims', true)::jsonb ->> 'role' $$;
      grant usage on schema public, auth to anon, authenticated, service_role;
      grant execute on function auth.uid(), auth.jwt(), auth.role() to anon, authenticated, service_role;
    `);
    const migrations = (await readdir(resolve(ROOT, 'supabase/migrations')))
      .filter(name => name.endsWith('.sql')).sort();
    assert.ok(migrations.length, 'No database migrations found');
    for (const name of migrations) await db.exec(await readFile(resolve(ROOT, 'supabase/migrations', name), 'utf8'));
    const seed = await readFile(resolve(ROOT, 'supabase/seed.sql'), 'utf8');
    await db.exec(seed);
    await db.exec(seed); // Catalogue updates must be idempotent.
    await db.query(`insert into auth.users (id,email,raw_user_meta_data) values
      ($1,'alice@example.test','{"display_name":"Alice"}'),
      ($2,'bob@example.test','{"display_name":"Bob"}')`, [ALICE, BOB]);
    check('Auth inserts create owner profiles', Number(await scalar('select count(*) from public.profiles')), 2);
    const playlist = JSON.parse(await readFile(resolve(ROOT, 'playlist.json'), 'utf8'));
    const tracks = await anon(() => db.query(`select t.id,t.title,m.public_url as audio_url
      from public.tracks t join public.media_assets m on m.id=t.audio_asset_id order by t.sort_order`));
    check('Seed preserves all five canonical R2 track IDs and URLs', tracks.rows,
      playlist.tracks.map(({id,title,audio_url}) => ({id,title,audio_url})));
    check('All 13 application tables have RLS', Number(await scalar(`select count(*) from pg_class c
      join pg_namespace n on n.oid=c.relnamespace where n.nspname='public' and c.relkind='r' and c.relrowsecurity`)), 13);
    const room = await alice(() => scalar("select public.create_room('starter_club','Alice club','freewill')"));
    const layout = await alice(() => scalar('select id from public.room_layouts where room_id=$1', [room]));
    check('Creator can read their room', Number(await alice(() => scalar('select count(*) from public.rooms'))), 1);
    check('Other account cannot read private room', Number(await bob(() => scalar('select count(*) from public.rooms'))), 0);
    check('Other account cannot read private placements', Number(await bob(() => scalar('select count(*) from public.room_placements'))), 0);
    await denied('Anonymous cannot create rooms', () => anon(() => scalar("select public.create_room('starter_club','No login')")));
    await denied('Owner cannot directly rewrite room ownership', () => alice(() => db.query('update public.rooms set owner_id=$1 where id=$2', [BOB,room])));
    await denied('Owner cannot self-grant inventory', () => alice(() => db.query("insert into public.user_inventory(user_id,item_id) values($1,'poster_freewill')", [ALICE])));
    await denied('Client cannot rewrite catalogue audio URLs', () => alice(() => db.query("update public.media_assets set public_url='https://example.test/changed.mp3'")));
    await denied('Client cannot call private validation helper', () => alice(() => db.query('select simbar_private.validate_layout($1)',[layout])));
    await denied('Other account cannot save owner layout', () => bob(() => scalar(
      'select public.save_layout($1,0,$2::jsonb,$3::jsonb,$4::jsonb)', [layout,'[]','[]','{}'])));
    const placements = [{key:'poster-1',item_id:'poster_freewill',zone:'wall',x:15,y:25,rotation:0,scale:1,options:{}}];
    const save = (revision, placed = placements, orderedTracks = ['track_001','track_004'], settings = {lighting:'warm',ambience:'quiet'}) =>
      alice(() => scalar('select public.save_layout($1,$2,$3::jsonb,$4::jsonb,$5::jsonb)',
        [layout,revision,JSON.stringify(placed),JSON.stringify(orderedTracks),JSON.stringify(settings)]));
    const revision = Number(await save(0));
    check('First save increments revision', revision, 1);
    await denied('Stale revision cannot overwrite layout', () => save(0), '40001');
    await denied('Unknown item cannot be placed', () => save(revision,[{...placements[0],item_id:'missing_item'}]), '23503');
    await denied('Out-of-bounds placement is rejected', () => save(revision,[{...placements[0],x:101}]), '22023');
    await denied('Wrong placement zone is rejected', () => save(revision,[{...placements[0],zone:'floor'}]), '22023');
    await denied('Duplicate playlist track is rejected', () => save(revision,placements,['track_001','track_001']), '23505');
    await denied('Unknown settings are rejected', () => save(revision,placements,['track_001'],{admin:true}), '22023');
    await denied('Explicit null rotation is rejected before numeric cast', () => save(revision,[{...placements[0],rotation:null}]), '22023');
    await denied('Non-numeric coordinates are rejected', () => save(revision,[{...placements[0],x:'NaN'}]), '22023');
    await denied('Unknown item options are rejected', () => save(revision,[{...placements[0],options:{script:'unsafe'}}]), '22023');
    await denied('Incorrect option value type is rejected', () => save(revision,[{...placements[0],options:{flip_x:'yes'}}]), '22023');
    await denied('Template placement limit is enforced', () => save(revision,
      Array.from({length:65},(_,index)=>({...placements[0],key:`poster-${index}`}))), '22023');
    await db.query(`insert into public.items(id,name,category,allowed_zones,is_free)
      values('test_locked_poster','Locked fixture','poster',array['wall'],false)`);
    await denied('Non-free item requires owned inventory', () => save(revision,[{...placements[0],item_id:'test_locked_poster'}]), '22023');
    await db.query(`insert into public.media_assets(id,kind,provider,object_key,public_url,active)
      values('test_inactive_image','image','external','test-image','https://example.test/image.png',false)`);
    await db.query(`insert into public.items(id,name,category,asset_id,allowed_zones,is_free)
      values('test_inactive_asset_item','Inactive asset fixture','poster','test_inactive_image',array['wall'],true)`);
    await denied('Items with inactive rendering assets cannot be placed', () => save(revision,[{...placements[0],item_id:'test_inactive_asset_item'}]), '22023');
    check('Failed saves retain revision', Number(await alice(() => scalar('select revision from public.room_layouts where id=$1',[layout]))), 1);
    check('Failed save rolls back placement replacements',
      Number(await alice(() => scalar('select x from public.room_placements where layout_id=$1',[layout]))), 15);
    check('Failed save rolls back playlist replacements',
      (await alice(() => db.query('select track_id from public.layout_tracks where layout_id=$1 order by position',[layout]))).rows.map(row=>row.track_id),
      ['track_001','track_004']);
    await denied('Other account cannot publish room', () => bob(() => scalar('select public.publish_room($1,1)',[room])));
    await denied('Stale revision cannot publish room', () => alice(() => scalar('select public.publish_room($1,0)',[room])), '40001');
    const slug = await alice(() => scalar("select public.publish_room($1,1,'public')",[room]));
    const published = await anon(() => scalar('select public.get_published_room($1)',[slug]));
    check('Published snapshot has schema version', published.snapshot.schema_version, 1);
    check('Published snapshot preserves ordered tracks', published.snapshot.layout.tracks.map(track=>track.id), ['track_001','track_004']);
    check('Published snapshot exposes R2 audio URL', published.snapshot.layout.tracks[0].audio_url, playlist.tracks[0].audio_url);
    check('Shared creator contains display name only', Object.keys(published.snapshot.creator), ['display_name']);
    const listings = await anon(() => scalar('select public.list_public_rooms()'));
    check('Public directory includes published room', listings.some(entry=>entry.slug===slug), true);
    await db.query("update public.items set name='Changed poster catalogue label' where id='poster_freewill'");
    await db.query("update public.room_templates set version=version+1,definition=definition || '{\"test_marker\":true}'::jsonb where id='starter_club'");
    const afterCatalogueEdit = await anon(() => scalar('select public.get_published_room($1)',[slug]));
    check('Catalogue and template edits cannot mutate an existing snapshot', afterCatalogueEdit.snapshot, published.snapshot);
    check('Snapshot captures original template definition', published.snapshot.template.definition.test_marker, undefined);
    check('Snapshot captures original placement catalogue label',
      published.snapshot.layout.placements[0].item.name === 'Changed poster catalogue label', false);
    await denied('Anonymous cannot enumerate publication table', () => anon(() => db.query('select * from public.room_publications')));
    await denied('Anonymous cannot inspect visit identities', () => anon(() => db.query('select * from public.room_visits')));
    await denied('Authenticated visitors cannot inspect visit identities', () => bob(() => db.query('select * from public.room_visits')));
    const secondRevision = Number(await save(1,[{...placements[0],x:65}],['track_004']));
    check('Later save increments revision', secondRevision, 2);
    const unchanged = await anon(() => scalar('select public.get_published_room($1)',[slug]));
    check('Editing draft does not change published snapshot', unchanged.snapshot, published.snapshot);
    check('Owner visits do not inflate visit total', Number(await alice(() => scalar('select public.record_visit($1,$2)',[slug,SESSION]))), 0);
    check('First visit counts once', Number(await anon(() => scalar('select public.record_visit($1,$2)',[slug,SESSION]))), 1);
    check('Duplicate visit is idempotent', Number(await anon(() => scalar('select public.record_visit($1,$2)',[slug,SESSION]))), 1);
    check('Published visit total is aggregated', Number((await anon(() => scalar('select public.get_published_room($1)',[slug]))).visit_count), 1);
    const unlisted = await alice(() => scalar("select public.publish_room($1,2,'unlisted')",[room]));
    const unlistedRoom = await anon(() => scalar('select public.get_published_room($1)',[unlisted]));
    check('Unlisted room is available by exact slug', unlistedRoom.visibility, 'unlisted');
    check('Unlisted room is excluded from public directory', (await anon(() => scalar('select public.list_public_rooms()'))).length, 0);
    check('Superseded snapshot link is no longer published', await anon(() => scalar('select public.get_published_room($1)',[slug])), null);
    await alice(() => db.query('select public.unpublish_room($1)',[room]));
    check('Unpublished room is inaccessible publicly', await anon(() => scalar('select public.get_published_room($1)',[unlisted])), null);
    await denied('Unpublished room cannot accept visits', () => anon(() => scalar('select public.record_visit($1,$2)',[unlisted,SESSION])), 'P0002');
    await db.query("insert into public.user_inventory(user_id,item_id) values($1,'test_locked_poster')",[ALICE]);
    check('Administrative unlock permits owned item', Number(await save(2,[{...placements[0],item_id:'test_locked_poster'}])), 3);
    await denied('Other user cannot create owner presets', () => bob(() => scalar("select public.create_layout($1,'Other preset')",[room])));
    const secondLayout = await alice(() => scalar("select public.create_layout($1,'Second preset')",[room]));
    await denied('Multiple presets require explicit publication selection', () => alice(() => scalar("select public.publish_room($1,3,'public')",[room])), '22023');
    const secondSlug = await alice(() => scalar("select public.publish_room($1,0,'public',$2)",[room,secondLayout]));
    check('Explicit preset selection publishes chosen revision', (await anon(() => scalar('select public.get_published_room($1)',[secondSlug]))).snapshot.layout.revision, 0);
    const beforeRename = await anon(() => scalar('select public.get_published_room($1)',[secondSlug]));
    await denied('Anonymous cannot update a profile', () => anon(() => db.query("select public.update_profile('No login')")));
    await denied('Blank profile display name is rejected', () => alice(() => db.query("select public.update_profile('  ')")), '22023');
    await denied('Oversized profile display name is rejected', () => alice(() => db.query('select public.update_profile($1)',['x'.repeat(41)])), '22023');
    await alice(() => db.query("select public.update_profile('  Alice renamed  ')"));
    check('Owner can update trimmed display name', await alice(() => scalar('select display_name from public.profiles where id=$1',[ALICE])), 'Alice renamed');
    check('Profile update does not modify other accounts', await bob(() => scalar('select display_name from public.profiles where id=$1',[BOB])), 'Bob');
    await denied('Other user cannot rename a room', () => bob(() => db.query("select public.rename_room($1,'Intruder')",[room])));
    await denied('Blank room title is rejected', () => alice(() => db.query("select public.rename_room($1,'  ')",[room])), '22023');
    await denied('Oversized room title is rejected', () => alice(() => db.query('select public.rename_room($1,$2)',[room,'x'.repeat(81)])), '22023');
    await alice(() => db.query("select public.rename_room($1,'  Renamed club  ')",[room]));
    check('Owner can rename room', await alice(() => scalar('select title from public.rooms where id=$1',[room])), 'Renamed club');
    await denied('Other user cannot rename a preset', () => bob(() => db.query("select public.rename_layout($1,'Intruder')",[secondLayout])));
    await denied('Blank preset name is rejected', () => alice(() => db.query("select public.rename_layout($1,'  ')",[secondLayout])), '22023');
    await denied('Oversized preset name is rejected', () => alice(() => db.query('select public.rename_layout($1,$2)',[secondLayout,'x'.repeat(61)])), '22023');
    await alice(() => db.query("select public.rename_layout($1,'  Renamed preset  ')",[secondLayout]));
    check('Owner can rename preset', await alice(() => scalar('select name from public.room_layouts where id=$1',[secondLayout])), 'Renamed preset');
    check('Profile and room renames do not alter published snapshot',
      (await anon(() => scalar('select public.get_published_room($1)',[secondSlug]))).snapshot, beforeRename.snapshot);
    await denied('Other user cannot delete a room', () => bob(() => db.query('select public.delete_room($1)',[room])));
    await denied('Other user cannot delete a preset', () => bob(() => db.query('select public.delete_layout($1)',[layout])));
    await denied('Currently published preset cannot be deleted', () => alice(() => db.query('select public.delete_layout($1)',[secondLayout])), '22023');
    const historicalSnapshots = (await db.query('select id,snapshot from public.room_publications where layout_id=$1 order by id',[layout])).rows;
    await alice(() => db.query('select public.delete_layout($1)',[layout]));
    check('Deleting old preset removes its placements', Number(await scalar('select count(*) from public.room_placements where layout_id=$1',[layout])), 0);
    check('Deleting old preset preserves immutable publication history',
      (await db.query('select id,snapshot from public.room_publications where id=any($1::uuid[]) order by id',[historicalSnapshots.map(row=>row.id)])).rows,
      historicalSnapshots);
    check('Deleted preset is detached from historical publications', Number(await scalar('select count(*) from public.room_publications where layout_id=$1',[layout])), 0);
    await alice(() => db.query('select public.unpublish_room($1)',[room]));
    await denied('Last remaining preset cannot be deleted', () => alice(() => db.query('select public.delete_layout($1)',[secondLayout])), '22023');
    const extras = [];
    for(let index=0;index<19;index++) extras.push(await alice(() => scalar('select public.create_layout($1,$2)',[room,`Extra ${index}`])));
    check('Room can hold twenty presets', Number(await alice(() => scalar('select count(*) from public.room_layouts where room_id=$1',[room]))), 20);
    await denied('Twenty-preset capacity is enforced', () => alice(() => scalar("select public.create_layout($1,'Over limit')",[room])), '22023');
    await alice(() => db.query('select public.delete_layout($1)',[extras[0]]));
    await alice(() => scalar("select public.create_layout($1,'Replacement')",[room]));
    check('Deleting a preset frees capacity', Number(await alice(() => scalar('select count(*) from public.room_layouts where room_id=$1',[room]))), 20);
    const finalSlug = await alice(() => scalar("select public.publish_room($1,0,'public',$2)",[room,secondLayout]));
    const renamedPublication = await anon(() => scalar('select public.get_published_room($1)',[finalSlug]));
    check('Republishing captures new room title', renamedPublication.snapshot.room.title, 'Renamed club');
    check('Republishing captures new creator name', renamedPublication.snapshot.creator.display_name, 'Alice renamed');
    await anon(() => scalar('select public.record_visit($1,$2)',[finalSlug,SESSION]));
    await alice(() => db.query('select public.delete_room($1)',[room]));
    check('Owner can delete a published room', Number(await scalar('select count(*) from public.rooms where id=$1',[room])), 0);
    check('Room deletion cascades all presets', Number(await scalar('select count(*) from public.room_layouts where room_id=$1',[room])), 0);
    check('Room deletion cascades publication history', Number(await scalar('select count(*) from public.room_publications where room_id=$1',[room])), 0);
    check('Room deletion cascades visit rows', Number(await scalar('select count(*) from public.room_visits')), 0);
    check('Deleted room share link is inaccessible', await anon(() => scalar('select public.get_published_room($1)',[finalSlug])), null);
    const result = {engine:'PostgreSQL WASM / PGlite', migrations, passed:checks.length, checks};
    await mkdir(resolve(ROOT,'Temp/SupabaseDatabaseTests'),{recursive:true});
    await writeFile(resolve(ROOT,'Temp/SupabaseDatabaseTests/results.json'),JSON.stringify(result,null,2)+'\n');
    return result;
  } finally { await db.close(); }
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const result = await runSupabaseDatabaseTests();
  console.log(`Supabase database: ${result.passed} checks passed (${result.engine}).`);
}
