const pair=(en,ko)=>[en,ko];
const scenes={
  freewill:[
    {title:pair('First time at Pet Sounds','처음 온 손님들'),jaiden:pair('Sounds good. I’ll pick what the bar recommends for the first round.','좋아요. 그동안 바에서는 첫 잔으로 뭘 권할지 제가 정할게요.'),band:pair('A lot of people are seeing us for the first time. Can we say a quick hello before the first song?','오늘 저희 처음 보는 분들이 많대요. 첫 곡 전에 짧게 인사하고 시작할까요?'),options:[{label:pair('Recommend Faust','파우스트 추천'),item:0},{label:pair('Recommend Adios Motherfucker','아디오스 마더퍼커 추천'),item:1}]},
    {title:pair('The regulars are back','다시 찾아온 단골들'),jaiden:pair('Good to have them back. I’ll choose what the bar recommends first tonight.','다시 와주셔서 반갑네요. 오늘 첫 잔은 바에서 제가 정할게요.'),band:pair('I recognize a few faces from our first show. Let’s open with something they know.','첫 공연 때 봤던 얼굴이 몇 분 계시네요. 다들 아는 곡으로 시작할게요.'),options:[{label:pair('Recommend Faust','파우스트 추천'),item:0},{label:pair('Recommend Adios Motherfucker','아디오스 마더퍼커 추천'),item:1}]}
  ],
  july:[
    {title:pair('While the crowd settles','공연 전 여유'),jaiden:pair('I’ll have the floor staff suggest something to share to the tables that are waiting.','그럼 기다리는 테이블에 나눠 먹기 좋은 메뉴부터 안내할게요.'),band:pair('People are still getting settled. We’ll say a quick hello before the first song.','아직 자리 잡는 분들이 있네요. 첫 곡 들어가기 전에 잠깐 인사할게요.'),options:[{label:pair('Recommend half pepperoni pizza','페퍼로니 피자 하프 추천'),item:2},{label:pair('Recommend fried chicken','후라이드 치킨 추천'),item:5}]},
    {title:pair('A quick bite before the show','첫 곡 전에 가벼운 한입'),jaiden:pair('Understood. I’ll have the kitchen suggest a quick bite to anyone who’s already seated.','좋아요. 먼저 앉은 손님들께는 주방에서 바로 나갈 메뉴를 제가 권할게요.'),band:pair('We’re tuned up. Give us the signal when the room is ready and we’ll start.','튜닝 끝났어요. 객석 정리되면 신호 주세요. 바로 들어갈게요.'),options:[{label:pair('Chili cheese fries','칠리 치즈 프라이'),item:3},{label:pair('Chili cheese chips','칠리 치즈 칩스'),item:4}]}
  ],
  double:[
    {title:pair('One room, two bands','두 팀, 한 객석'),jaiden:pair('Some fans are here for only one band. How should we bring everyone into the second set?','한 팀만 보러 온 손님도 있어요. 2부에선 어떻게 함께 맞이할까요?'),band:pair('Freewill can hand over the mic, or you can greet both crowds together.','프리윌이 마이크로 이어줘도 좋고, 사장님이 두 팀의 손님을 함께 맞아줘도 좋죠.'),options:[{label:pair('Freewill hands over','프리윌이 바통 넘기기'),speaker:'freewill'},{label:pair('Jaiden greets the room','제이든이 관객 맞이'),speaker:'owner'}]}
  ]
};
const getScene=p=>{
  const group=Object.hasOwn(scenes,p?.showId)?scenes[p.showId]:null;
  if(p?.day!==4||!Number.isInteger(p.encoreSeed)||p.encoreSeed<=0||p.encoreSeed>0xffffffff||!group)return null;
  const index=p.showId==='july'?(p.encoreSeed>>>1)%group.length:p.encoreSeed%group.length;
  return group[index];
};
export const Encore={
  scene:getScene,
  effect:p=>(p?.encoreChoice===0||p?.encoreChoice===1)?getScene(p)?.options[p.encoreChoice]:null
};
