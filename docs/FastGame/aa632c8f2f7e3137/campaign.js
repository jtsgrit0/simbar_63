const whole=(value,min=0,max=1e9)=>Math.trunc(Math.max(min,Math.min(max,Number.isFinite(+value)?+value:min)));
const shows=[
  {id:'freewill',ko:'프리윌 라이브',en:'Freewill Live',bandIndices:[24],tracks:[0,3,4],fee:120,draw:20,tipMultiplier:1,orderIntervalMultiplier:1.08,preferredItems:[0,1],
    descriptionKo:'칵테일을 즐기는 관객과 여유 있는 주문. 작은 무대의 첫 공연에 어울립니다.',descriptionEn:'A cocktail crowd and a gentler service pace. A welcoming first show for a small venue.'},
  {id:'july',ko:'줄라이원츠모어 라이브',en:'JulyWantsMore Live',bandIndices:[25],tracks:[1,2],fee:180,draw:28,tipMultiplier:1.18,orderIntervalMultiplier:.94,preferredItems:[2,3,5],
    descriptionKo:'안주를 찾는 관객과 팁 +18%. 첫 영업을 마치면 섭외할 수 있습니다.',descriptionEn:'A food-loving crowd with 18% better tips. Available after completing your first night.'},
  {id:'double',ko:'프리윌 × 줄라이 합동 공연',en:'Freewill × July Double Bill',bandIndices:[24,25],tracks:[0,1,2,3,4],fee:320,draw:40,tipMultiplier:1.12,orderIntervalMultiplier:.82,preferredItems:[0,5],
    descriptionKo:'두 팀의 팬이 모이는 빠른 영업. 영업 2회 완료와 음향·조명 1단계가 필요합니다.',descriptionEn:'A busier night for both bands’ fans. Requires two completed nights and level-one sound and lights.'}
].map(show=>Object.freeze({...show,bandIndices:Object.freeze(show.bandIndices),tracks:Object.freeze(show.tracks),preferredItems:Object.freeze(show.preferredItems)}));

/** Small authored campaign rules shared by planning, service and its UI. */
export const Campaign={
  shows:Object.freeze(shows),
  show:id=>shows.find(show=>show.id===id)||null,
  stage:completedNights=>Math.min(3,whole(completedNights)+1),
  targets(stage,challenge=false){
    const base=[[8,2,3],[12,3,4],[16,4,5]][whole(stage,1,3)-1];
    return base.map((value,index)=>value+(challenge?[6,2,2][index]:0));
  },
  purchaseGate(type,index,nextLevel,completedNights){
    let requiredNights=9999;
    if(Number.isInteger(index)&&index>=0&&index<3){
      if(type==='hire'||type==='staff')requiredNights=index===2?2:1;
      else if((type==='upgrade'||type==='facility')&&Number.isInteger(nextLevel)&&nextLevel>=1&&nextLevel<=3)requiredNights=[1,3,5][nextLevel-1];
    }
    return{allowed:whole(completedNights)>=requiredNights,requiredNights};
  }
};
