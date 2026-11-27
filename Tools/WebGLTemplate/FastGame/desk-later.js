import {menuForOwner} from './petsounds-menu.js';
const menu=menuForOwner(2);
const pair=(en,ko)=>[en,ko];
const options=(names)=>names.map(key=>{
  const item=menu.find(entry=>entry.en===key);
  if(!item)throw new Error('Unknown Pet Sounds recommendation: '+key);
  return{label:pair(item.en,item.ko),item:item.index};
});
const data={
  freewill:[
    ["첫 곡을 지켜보는 손님","First song, new faces","오늘은 처음 들어보는 분들이 많아서 첫 곡에 바로 들어갈게요.","Lots of new faces. We’ll go straight into the first song.","좋아요. 첫 주문이 몰리기 전에 바에서 추천할 한 잔을 정해둘게요.","I’ll pick a house recommendation before the first rush.","FAUST","Kelly Draft"],
    ["앰프가 식기 전에","While the amps are warm","전환 때 소리 끊기지 않게 다음 곡까지 바로 이어도 될까요?","Can we run straight into the next song without a break?","좋아요. 바에서도 빠르게 나갈 한 잔을 안내할게요.","Yes. I’ll have the bar suggest a quick first drink.","Guinness Draft","Long Island Iced Tea"],
    ["앞줄 정리","Make room up front","앞줄이 금방 찰 것 같아요. 통로만 비워두면 좋겠어요.","The front might fill quickly. Could we keep the aisle clear?","홀에 전달할게요. 먼저 온 손님에게는 이 중 한 잔을 권할까요?","I’ll tell the floor team. Which drink should the bar suggest?","ADIOS MOTHERFUCKER","Peach Crush"],
    ["익숙한 후렴","A familiar chorus","지난번 따라 부르던 분들이 또 오신대요. 그 곡을 초반에 넣을게요.","Some singalong regulars are back. We’ll play their song early.","그럼 바도 바로 준비해둘게요. 오늘 추천은 어느 쪽으로 갈까요?","The bar will be ready. Which drink should we feature tonight?","Cuba Libre","Aperol Spritz"],
    ["조용한 시작","A quiet opening","첫 곡은 한 박자 쉬고 시작하고 싶어요. 객석 소리가 들리게요.","We’d like one quiet beat before the first song.","좋아요. 술 안 드시는 손님도 고를 수 있게 추천을 정해볼게요.","Let’s have a recommendation ready for guests who don’t drink.","Nojito","A Free Day Pale Ale"],
    ["관객과 가까이","Closer to the crowd","오늘은 마이크 스탠드를 조금 앞으로 뺄게요. 관객 얼굴이 잘 보이게요.","I’ll move the mic stand forward so I can see the crowd.","좋아요, 동선은 제가 볼게요. 바에서는 어떤 걸 먼저 안내할까요?","I’ll watch the floor. What should the bar suggest first?","Midori Sour","Blue Hawaiian"],
    ["장비 점검 끝","Soundcheck is done","기타 톤 잡혔어요. 문 열리면 바로 올라갈 수 있어요.","The guitar tone is ready. We can go up as soon as doors open.","그럼 먼저 온 손님들께 바에서 추천을 하나 정해둘게요.","Then I’ll choose a recommendation for the early arrivals.","Negroni","Whisky Sour"],
    ["첫 곡 전 환호","A cheer before the first song","첫 곡 전에 오늘 누구 보러 왔냐고 객석에 물어보고 싶어요.","Before the first song, I want to ask who everyone came to see.","좋죠. 기다리는 손님에게는 바에서 한 잔을 먼저 권할게요.","Sounds good. The bar can suggest a first drink while they wait.","Tequila Sunrise","Apple Martini"],
    ["짧은 사운드체크","A quick soundcheck","오늘은 체크를 짧게 끝내고 관객 입장 준비 시간을 더 드릴게요.","We’ll keep soundcheck short so you have more time before doors.","고마워요. 그동안 입장 손님에게 권할 메뉴를 정하죠.","Thanks. Let’s decide what to recommend at the bar.","Classic Mojito","Fantasy"],
    ["다시 올라온 무대","Back on this stage","이번에는 객석부터 한 번 보고 시작할게요. 인사는 곡 끝나고 할게요.","We’ll take a look at the room first and say hello after the song.","좋아요. 그럼 그 사이 바에서 권할 첫 잔을 준비해둘게요.","I’ll get the bar’s first recommendation ready for then.","Dark and Stormy","Moscow Mule"]
  ],
  july:[
    ["먼저 자리 잡은 손님들","Early tables","먼저 앉은 분들이 벌써 무대를 보고 있네요. 첫 곡은 편하게 시작할게요.","The early tables are already watching. We’ll open gently.","좋아요. 자리 잡은 테이블에 나눠 먹을 메뉴를 안내할게요.","I’ll have the floor suggest something the tables can share.","Pepperoni Pizza","Wings & Wedges"],
    ["곡 사이 한마디","A word between songs","두 번째 곡 전에 새 곡 얘기를 잠깐 하고 싶어요.","I’d like to say a little about the new song before the second one.","그 사이엔 주문 내기 편한 주방 메뉴를 추천할게요.","I’ll suggest something the kitchen can serve easily in that gap.","Olives","French Fries"],
    ["스피커 가까운 자리","The tables by the speakers","스피커 앞 테이블이 가깝네요. 첫 곡 볼륨을 한 단계 낮춰 볼게요.","Those tables are close to the speakers. We’ll start a little quieter.","좋아요, 제가 그 테이블부터 살펴볼게요. 주방 추천은 이 중에 정하죠.","I’ll check those tables first. Let’s choose the kitchen recommendation.","Cheese & Salami","Grilled Sausage"],
    ["아직 빈 테이블","A few tables still open","빈자리가 조금 보여요. 문 쪽 손님들이 들어오기까지 인사를 길게 할까요?","There are still a few empty tables. Shall we give arrivals a moment?","부탁해요. 먼저 앉은 손님께는 반판 메뉴를 하나 안내할게요.","Please do. I’ll recommend a half pizza to the seated guests.","Hawaiian Half Pizza","Cheese Half Pizza"],
    ["익숙한 곡으로","Start with a familiar tune","첫 곡을 아는 분들이 많을 것 같아서 셋리스트 순서를 바꿔봤어요.","We swapped the setlist so we can open with a familiar song.","좋아요. 단골 테이블에는 오늘 주방 추천도 같이 전할게요.","I’ll pass tonight’s kitchen recommendation to the regulars.","CHILI CHEESE FRIES","Sweet Fish Jerky & Peanuts"],
    ["창가 테이블","The window tables","창가 쪽은 무대가 살짝 비껴 보여요. 첫 인사는 그쪽도 보고 할게요.","The window tables are off to the side. I’ll greet them too.","고마워요. 창가 손님께 같이 먹기 좋은 메뉴를 하나 권하죠.","Thanks. Let’s recommend something to share for those tables.","Hawaiian Pizza","Chicken Tenders & Fries"],
    ["촬영보다 공연","Be here for the song","첫 곡은 화면 말고 맨눈으로 봐달라고 인사하면서 부탁하려고요.","In the intro, I’ll ask people to watch the first song with their own eyes.","좋아요. 그 전에 자리 잡은 손님께는 간단한 안주를 안내할게요.","I’ll suggest a light snack to the people already seated.","Nacho Chips","Wedge Potatoes"],
    ["오늘의 신곡","The new song","오늘 신곡은 중간에 넣을게요. 앞 두 곡은 편하게 듣는 곡으로 골랐어요.","The new song goes in the middle. We’ve chosen two easy openers.","그럼 바쁜 시간 전에 주방 추천을 먼저 알릴게요.","I’ll announce the kitchen recommendation ahead of the rush.","Painkiller Pizza","Cheese Pizza"],
    ["무대 뒤 마지막 확인","One last check backstage","코러스 한 번만 맞춰보고 나면 바로 무대에 오를 수 있어요.","One last chorus check and we’re ready for the stage.","천천히 하세요. 기다리는 테이블엔 가볍게 먹을 걸 제안할게요.","Take your time. I’ll suggest something small to the waiting tables.","Pepperoni Half Pizza","CHILI CHEESE CHIPS"],
    ["첫 곡 끝나고 인사","A hello after the opener","오늘은 첫 곡부터 들려드리고, 끝나면 팀 소개할게요.","We’ll play the opener first and introduce ourselves afterward.","좋아요. 홀에 알리고 주방 추천 메뉴도 하나 정할게요.","I’ll tell the floor team and pick a kitchen recommendation.","Painkiller Half Pizza","FRIED CHICKEN"]
  ]
};
const scenes=Object.fromEntries(Object.entries(data).map(([id,rows])=>[id,rows.map(([ko,en,bk,be,jk,je,...menu])=>({
  title:pair(en,ko),band:pair(be,bk),jaiden:pair(je,jk),options:options(menu)
}))]));
scenes.double=[{
  title:pair('Share the stage','같은 무대, 두 팀'),
  jaiden:pair('Let’s give the second band a proper welcome. Who takes the mic?','2부가 시작될 때 두 팀 손님을 같이 맞이하죠. 누가 마이크를 잡을까요?'),
  band:pair('Freewill can make the handoff, or you can welcome both crowds.','프리윌이 무대에서 넘겨도 좋아요. 사장님이 객석 전체에 인사해도 좋고요.'),
  options:[{label:pair('Freewill hands over','프리윌이 바통 넘기기'),speaker:'freewill'},{label:pair('Jaiden welcomes both crowds','제이든이 관객 맞이'),speaker:'owner'}]
}];
const solo=['freewill','july'];
const full=(1<<10)-1;
const valid=i=>Number.isInteger(i)&&i>=0&&i<10;
const enabled=p=>p?.day>=5&&p.deskVersion===2&&p.campaignContext?.venueOwner===2;
function sanitizeHistory(value){
  return Object.fromEntries(solo.map(id=>{
    const row=value?.[id],mask=Number.isInteger(row?.mask)&&row.mask>=0&&row.mask<=full?row.mask:0;
    return[id,{mask,last:valid(row?.last)&&(mask&(1<<row.last))?row.last:-1}];
  }));
}
function sanitizePlan(p){
  if(!enabled(p)){delete p.laterScenes;return;}
  if(!p.laterScenes||typeof p.laterScenes!=='object'){delete p.laterScenes;return;}
  p.laterScenes=Object.fromEntries(solo.filter(id=>valid(p.laterScenes[id])).map(id=>[id,p.laterScenes[id]]));
}
function prepare(p,value,rng){
  if(!enabled(p)||p.locked||p.completed)return;
  sanitizePlan(p);p.laterScenes??={};
  const history=sanitizeHistory(value);
  for(const id of solo)if(!valid(p.laterScenes[id])){
    const {mask,last}=history[id],choices=[];
    for(let i=0;i<10;i++)if(mask===full?i!==last:!(mask&(1<<i)))choices.push(i);
    p.laterScenes[id]=choices[Math.min(choices.length-1,Math.max(0,Math.floor(rng()*choices.length)))];
  }
}
function scene(p){
  if(!enabled(p))return null;
  if(p.showId==='double')return scenes.double[0];
  const index=p.laterScenes?.[p.showId];
  return solo.includes(p.showId)&&valid(index)?scenes[p.showId][index]:null;
}
function consume(p,value){
  const history=sanitizeHistory(value),id=p?.showId;
  if(!p?.locked||!solo.includes(id)||!scene(p))return history;
  const index=p.laterScenes[id],row=history[id];
  row.mask=(row.mask===full?0:row.mask)|(1<<index);row.last=index;
  return history;
}
export const LaterDesk={enabled,scene,prepare,consume,sanitizePlan,sanitizeHistory};
