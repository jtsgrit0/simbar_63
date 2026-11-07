import {launchPreparedDesk} from './native_test_workflow.mjs';
// Actual exported starts of two deterministic nights that regressed when
// greeting fans stayed home while cleanup and staff return ran concurrently.
import {writeFileSync} from 'node:fs';
import {SimBarGame} from './WebGLTemplate/FastGame/core.js';
for(const seed of [3,10]){
  const mem=new Map(),storage={getItem:k=>mem.get(k)||null,setItem:(k,v)=>mem.set(k,String(v))};
  const g=new SimBarGame({storage,seed});g.act('start');launchPreparedDesk(g);while(g.phase==='guide')g.act('guide');
  while(g.phase==='playing'){g.tick(.05);if(g.reaction.activeKind)g.act('quick',{kind:g.reaction.activeKind});}
  writeFileSync(new URL(`./fixtures/aftershow-seed-${seed}.json`,import.meta.url),g.exportSave()+'\n');
}
console.log('Wrote actual exported seed 3 and 10 closing-night fixtures');
