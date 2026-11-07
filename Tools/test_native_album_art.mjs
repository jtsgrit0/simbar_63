import assert from 'node:assert/strict';
import {AlbumArtLoader} from './WebGLTemplate/FastGame/album-art.js';

const tracks=[0,1,2,3,4].map(index=>({index,cover:'album-covers/'+([1,2].includes(index)?'july-'+index:'freewill')+'.png'}));
let fetches=0;const images=[];
class FakeImage{
  constructor(){images.push(this);this.loaded=new Promise((resolve,reject)=>{this.resolve=resolve;this.reject=reject;});}
  decode(){return this.loaded;}
}
const covers={},loader=new AlbumArtLoader({baseURL:'https://test.invalid/art/',covers,ImageClass:FakeImage,fetcher:async()=>{fetches++;return{ok:true,json:async()=>({version:1,tracks})};}});
assert.equal(fetches,0);assert.equal(images.length,0,'Startup construction must not request a jacket.');
const first=loader.ensure(0),alias=loader.ensure(3),second=loader.ensure(2);
await new Promise(resolve=>setTimeout(resolve,0));
assert.equal(fetches,1);assert.equal(images.length,2,'Shared-release songs must share one image decode.');
assert.equal(covers[0],undefined,'An undecoded image cannot reach the renderer.');
images.find(image=>image.src.endsWith('july-2.png')).resolve();await second;
assert.equal(covers[2],images[1]);
images.find(image=>image.src.endsWith('freewill.png')).resolve();await Promise.all([first,alias]);
assert.equal(covers[0],covers[3]);assert.equal(covers[3],covers[4]);
assert.equal(covers[2],images[1],'A late previous-song decode must not replace another song cover.');
await loader.ensure(4);assert.equal(images.length,2);
assert.equal(await loader.ensure(-1),null);

const pending=loader.ensure(1);await new Promise(resolve=>setTimeout(resolve,0));
loader.destroy();images[2].resolve();assert.equal(await pending,null);assert.equal(covers[1],undefined);
assert.equal(await loader.ensure(0),null);

let attempts=0,failures=0;
const retry=new AlbumArtLoader({baseURL:'https://test.invalid/art/',ImageClass:class{decode(){return Promise.resolve();}},onerror:()=>failures++,fetcher:async()=>({ok:++attempts>1,json:async()=>({version:1,tracks})})});
assert.equal(await retry.ensure(1),null);assert.equal(failures,1);
assert(await retry.ensure(1),'A user retry must recover an optional-manifest failure.');
retry.destroy();

let unsafeImages=0;
const safe=new AlbumArtLoader({baseURL:'https://test.invalid/art/',ImageClass:class{constructor(){unsafeImages++;}},fetcher:async()=>({ok:true,json:async()=>({version:1,tracks:[{index:0,cover:'https://other.invalid/cover.png'},{index:1,cover:'album-covers/../../secret.png'}]})})});
assert.equal(await safe.ensure(0),null);assert.equal(await safe.ensure(1),null);assert.equal(unsafeImages,0);safe.destroy();
console.log('PASS: no startup requests, shared releases, decoded-only/race-safe covers, destroy, optional failure/retry and local-only jacket paths');
