const lines={
  neutral:['A lot of guests tonight.','오늘 손님이 많네.'],
  smile:['First order is in!','첫 주문 들어왔어!'],
  laugh:['Ha, applause and orders!','하하, 박수 소리에 주문까지!'],
  smirk:['Feels like an encore tonight.','이 분위기면 앙코르도 나오겠어.'],
  surprised:['Wait, another order?','잠깐, 주문이 또 들어왔다고?'],
  concerned:['That guest has been waiting.','저쪽 손님, 꽤 오래 기다리셨어.'],
  angry:['Orders got mixed up. Check the first one.','주문이 섞였어. 앞 주문부터 확인하자.'],
  sad:['We might lose this guest.','손님이 기다리다 가시겠어…'],
  embarrassed:['Oops, let’s check that again.','앗, 주문을 다시 확인해 볼게.'],
  determined:['One order at a time.','밀린 주문부터 하나씩 처리하자.']
};
export function petOrderManager(game){
  if(game.owner!==2||game.phase!=='playing'||game.state.staff[1]!==1||game.state.wageDebt)return null;
  const j=game.journey,order=game.orders[0],count=game.orders.length;
  const patience=order?.patience??Infinity;
  const mood=game.lastResult?.code==='wrong_item'?'angry'
    :patience<9?'sad'
    :patience<26?'concerned'
    :game.lastResult?.code==='miss'?'embarrassed'
    :count>=4?'determined'
    :j.combo>=4?'laugh'
    :game.state.performance>=.68?'smirk'
    :count>=3&&j.orders>0?'surprised'
    :j.orders===0&&order?'smile':'neutral';
  return {mood,line:lines[mood][game.language==='en'?0:1]};
}
