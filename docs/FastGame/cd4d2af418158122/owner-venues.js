// Presentation follows the selected owner; booking venues and save IDs stay stable.
export const OWNER_VENUES=Object.freeze([
  Object.freeze({key:'freebird',file:'venue-freebird.png',ko:'클럽 프리버드',en:'Club Freebird'}),
  Object.freeze({key:'ff',file:'venue-ff.png',ko:'홍대 FF',en:'Hongdae FF'}),
  Object.freeze({key:'original',file:'venue.png',ko:'기존 베뉴',en:'Original venue'})
]);
export const ownerVenue=owner=>OWNER_VENUES[Number.isInteger(owner)&&owner>=0&&owner<3?owner:2];
export const VENUE_PROPS=Object.freeze({'standing-table':'standing-table.png','standing-stool':'standing-stool.png'});

// Gameplay crowd limits describe these compact rooms, rather than the real
// venues' licensed capacities. The audience builder uses only authored floors.
const zone=(key,x0,z0,x1,z1,weight=1)=>Object.freeze({key,x0,z0,x1,z1,weight});
const seat=(id,x,z,facing)=>Object.freeze({id,x,z,facing});
export const AUDIENCE_VENUES=Object.freeze([
  Object.freeze({key:'freebird',capacity:64,demandMultiplier:1,energy:.75,style:'open-floor',
    zones:Object.freeze([zone('front-floor',.7,-3,8.7,1.35,1.4),zone('open-floor',-8.6,-5.25,.3,1.05),zone('bar-side',-8.7,1.5,-6.75,5.3,.7)]),seats:Object.freeze([])}),
  Object.freeze({key:'ff',capacity:80,demandMultiplier:1.2,energy:1,style:'tables-dj',
    zones:Object.freeze([zone('stage-floor',.5,-5.25,8.7,1.35,1.3),zone('table-floor',-6.3,-5.25,-.55,1.35),zone('dj-tables',-8.6,2,-2.3,5.4,1.1)]),seats:Object.freeze([])}),
  Object.freeze({key:'original',capacity:44,demandMultiplier:.8,energy:.35,style:'seated-listening',
    zones:Object.freeze([zone('listening-floor',-.95,-5.25,8.7,1.45)]),
    seats:Object.freeze([
      seat('bar-seat-0',-7.55,-3.15,Math.PI/2),seat('bar-seat-1',-6,-3.15,-Math.PI/2),seat('bar-seat-2',-4.45,-3.15,Math.PI*.2),seat('bar-seat-3',-2.9,-3.15,-Math.PI*.2),
      seat('lounge-0',-8.55,6.4,-Math.PI/2),seat('lounge-1',-7,6.4,Math.PI/2),seat('lounge-2',-4.45,6.4,0),seat('lounge-3',-2.95,6.4,Math.PI/2),
      seat('lounge-4',-8.35,-6.5,-Math.PI/2),seat('lounge-5',-6.85,-6.5,Math.PI*.25),seat('lounge-6',-3.45,-6.5,-Math.PI*.25)
    ])})
]);
