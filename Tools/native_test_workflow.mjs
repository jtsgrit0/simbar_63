import assert from 'node:assert/strict';

// Exercise the same persisted actions as the desk UI before entering a night.
export function launchPreparedDesk(game){
  assert.equal(game.phase,'planning');
  assert.equal(game.act('deskSelect',{selected:true}).success,true);
  assert.equal(game.act('deskWork',{task:0}).success,true);
  assert.equal(game.act('deskWork',{task:1}).success,true);
  assert.equal(game.act('launch').success,true);
}
