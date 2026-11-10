// Presentation follows the selected owner; booking venues and save IDs stay stable.
export const OWNER_VENUES=Object.freeze([
  Object.freeze({key:'freebird',file:'venue-freebird.png',ko:'클럽 프리버드',en:'Club Freebird'}),
  Object.freeze({key:'ff',file:'venue-ff.png',ko:'홍대 FF',en:'Hongdae FF'}),
  Object.freeze({key:'original',file:'venue.png',ko:'기존 베뉴',en:'Original venue'})
]);
export const ownerVenue=owner=>OWNER_VENUES[Number.isInteger(owner)&&owner>=0&&owner<3?owner:2];
