// Exact signed Int32 schedule from SimBarPosterCatalog.CreateDailyPoster.
import {catalog} from './catalog.js';
export const DAILY_POSTER_INDEX=30;
export function createDailyPoster(day=1,venue=0){
  day=Math.max(1,Math.trunc(day)||1);venue=Math.max(0,Math.min(3,Math.trunc(venue)||0));
  const titles=['DAYLINE FRESH NIGHT','NEON AUDITION NIGHT','PAPER STAGE LIVE','ROOFTOP AFTERWORK'];
  const ko=['데이라인 신인 라이브','네온 오디션 나이트','페이퍼 스테이지 라이브','루프탑 애프터워크'];
  const seed=Math.imul(day,73856093)^Math.imul(venue,19349663),title=Math.abs(seed%4);
  const start=Math.abs(((Math.imul(seed,31)+day)|0)%12),id='daily-'+day+'-'+catalog.venues[venue].id;
  return{id,index:DAILY_POSTER_INDEX,venueId:catalog.venues[venue].id,date:'영업일 '+day,title:ko[title]+' / '+titles[title],
    sourceUrl:'',instagramUrl:'',imagePath:'__dynamic_daily__/day-'+day+'/venue-'+catalog.venues[venue].id+'.png',
    evidence:'가상 신인 라인업으로 매 영업일 새로 제작된 픽셀 포스터',handle:'@daily-lineup · 게임 생성 포스터',
    artistIndices:Array.from({length:3},(_,i)=>(start+(i*5+venue+day)%12)%12)};
}

// The daily poster is generated locally like the Unity poster: no startup request.
export function dailyPosterImage(poster,language='ko'){
  const escape=v=>String(v).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const colors=['#ecd7b7','#34585c','#d65b4d','#252b30'];
  let hash=0;for(const c of poster.id)hash=(Math.imul(hash,31)+c.charCodeAt(0))|0;
  let tiles='';for(let y=0;y<4;y++)for(let x=0;x<4;x++)tiles+=`<rect x="${x*128}" y="${y*128}" width="128" height="128" fill="${colors[(x+y+Math.abs(hash)%3)%4]}"/>`;
  const names=poster.artistIndices.map((index,i)=>`<text x="256" y="${280+i*55}" text-anchor="middle" fill="#ecd7b7" font-family="sans-serif" font-size="22">${escape(language==='en'?catalog.bands[index].nameEnglish:catalog.bands[index].name)}</text>`).join('');
  const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="512" height="640" viewBox="0 0 512 640"><rect width="512" height="640" fill="#ecd7b7"/>${tiles}<rect x="28" y="28" width="456" height="584" fill="none" stroke="#252b30" stroke-width="20"/><rect x="48" y="76" width="416" height="112" fill="#252b30"/><text x="256" y="125" text-anchor="middle" font-family="sans-serif" font-size="27" font-weight="bold" fill="#ecd7b7">${escape(poster.title.split(' / ')[1])}</text><text x="256" y="164" text-anchor="middle" fill="#ecd7b7" font-family="sans-serif" font-size="20">${escape(poster.date)} · ${escape(catalog.venues.find(v=>v.id===poster.venueId)?.nameEnglish)}</text><rect x="48" y="225" width="416" height="200" fill="#252b30"/>${names}<rect x="48" y="490" width="416" height="102" fill="#252b30"/><text x="256" y="530" text-anchor="middle" fill="#ecd7b7" font-family="sans-serif" font-size="18">FICTIONAL NEWCOMERS · ALL GENRES</text><text x="256" y="562" text-anchor="middle" fill="#ecd7b7" font-family="sans-serif" font-size="16">DAILY LINEUP / GAME GENERATED</text></svg>`;
  return'data:image/svg+xml;charset=utf-8,'+encodeURIComponent(svg);
}
