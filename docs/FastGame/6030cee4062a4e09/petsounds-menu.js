import {catalog} from './catalog.js';

const groups=[
  ["set","세트","Set menu",[[25,"생맥주 세트","Food & Beer Set","메인 메뉴 1개 + 켈리 생맥주 2잔","1 main dish + 2 Kelly drafts"]]],
  ["main","메인","Main dishes",[[15,"페퍼로니 피자","Pepperoni Pizza"],[15,"하와이안 피자","Hawaiian Pizza"],[15,"페인킬러 피자","Painkiller Pizza"],[15,"치즈 피자","Cheese Pizza"],[15,"닭날개와 반달감자","Wings & Wedges"],[15,"치킨 텐더 & 프라이","Chicken Tenders & Fries"],[15,"모둠소시지","Grilled Sausage"],[15,"치즈 앤 살라미","Cheese & Salami"],[15,"쥐포와 땅콩","Sweet Fish Jerky & Peanuts"]]],
  ["half","반판 피자","Half pizzas",[[10,"하와이안 피자 반판","Hawaiian Half Pizza"],[10,"페인킬러 피자 반판","Painkiller Half Pizza"],[10,"치즈 피자 반판","Cheese Half Pizza"]]],
  ["side","사이드","Sides",[[9,"올리브","Olives"],[9,"나초","Nacho Chips"],[9,"감자튀김","French Fries"],[9,"반달감자","Wedge Potatoes"]]],
  ["draft","생맥주","Draft beer",[[7,"켈리","Kelly Draft"],[10,"기네스","Guinness Draft"],[8,"핸드앤몰트 슬로우 IPA","Hand & Malt Slow IPA"],[8,"핸드앤몰트 위트 에일","Hand & Malt Weat Ale"]]],
  ["bottle","병맥주·사이더","Bottled beer & cider",[[8,"버드와이저","Budweiser"],[9,"코로나","Corona"],[9,"호가든","Hoegaarden"],[9,"스텔라","Stella Artois"],[9,"서머스비","Somersby"],[9,"코젤 다크","Kozel Dark"],[10,"하이네켄","Heineken"],[11,"빅웨이브","Big Wave"],[12,"포그커터","Fogcutter"]]],
  ["zeroBeer","논알코올 맥주","Non-alcoholic beer",[[10,"어 프리 데이 페일 에일","A Free Day Pale Ale"]]],
  ["tropical","트로피컬 칵테일","Tropical cocktails",[[12,"롱 아일랜드 아이스티","Long Island Iced Tea"],[11,"피치 크러시","Peach Crush"],[11,"미도리 사워","Midori Sour"],[12,"섹스 온 더 비치","Sex on the Beach"],[12,"준 벅","June Bug"],[13,"피나 콜라다","Pina Colada"],[12,"블루 하와이안","Blue Hawaiian"],[11,"데킬라 선라이즈","Tequila Sunrise"],[13,"애플 마티니","Apple Martini"],[15,"스트로베리 마르가리타","Strawberry Margarita"],[15,"망고 마르가리타","Mango Margarita"]]],
  ["refresh","리프레시 칵테일","Refresh cocktails",[[15,"클래식 모히토","Classic Mojito"],[14,"다크 앤 스토미","Dark and Stormy"],[13,"쿠바 리브레","Cuba Libre"],[13,"아페롤 스프리츠","Aperol Spritz"],[14,"카이피리냐","Caipirinha"]]],
  ["special","스페셜 칵테일","Special cocktails",[[14,"네그로니","Negroni"],[15,"블러디 메리","Bloody Mary"],[14,"모스코 뮬","Moscow Mule"],[14,"불바르디에","Boulevardier"]]],
  ["sweet","스위트 칵테일","Sweet cocktails",[[11,"깔루아 밀크","Kahlua Milk"],[11,"베일리스 밀크","Baileys Milk"],[13,"화이트 러시안","White Russian"],[13,"그래스호퍼","Grasshopper"]]],
  ["whiskey","위스키 칵테일","Whiskey cocktails",[[15,"올드 패션드","Old Fashioned"],[14,"갓파더","Godfather"],[14,"맨해튼","Manhattan"],[14,"러스티 네일","Rusty Nail"],[15,"위스키 사워","Whisky Sour"]]],
  ["other","기타 칵테일","Other cocktails",[[14,"클래식 마르가리타","Classic Margarita"],[14,"드라이 마티니","Dry Martini"],[14,"더티 마티니","Dirty Martini"],[14,"다이키리","Daiquiri"],[14,"코스모폴리탄","Cosmopolitan"],[14,"김렛","Gimlet"]]],
  ["zeroCocktail","논알코올 칵테일","Non-alcoholic cocktails",[[14,"노히토","Nojito"],[12,"버진 피나 콜라다","Virgin Pina Colada"],[12,"판타지","Fantasy"],[13,"골든 메달리스트","Golden Medalist"]]]
];
const legacyGroups=['special','tropical','half','main','main','legacy'];
const legacyPrices=[14,12,10,15,15,18];
const legacy=catalog.drinks.map((row,i)=>Object.freeze({...row,group:legacyGroups[i],price:legacyPrices[i],
  ...(i===2?{name:'페퍼로니 피자 반판',ko:'페퍼로니 피자 반판',nameEnglish:'Pepperoni Half Pizza',en:'Pepperoni Half Pizza'}:{}),
  ...(i===4?{name:'칠리 치즈 나초',ko:'칠리 치즈 나초'}:{})}));
const items=legacy.slice();
for(const [group,,,rows] of groups)for(const [price,ko,en,noteKo,noteEn] of rows){
  items.push(Object.freeze({index:items.length,group,price,isFood:['set','main','half','side'].includes(group),ko,en,name:ko,nameEnglish:en,noteKo,noteEn}));
}
const petMenu=Object.freeze(items);
export const PET_SOUNDS_MENU_GROUPS=Object.freeze([...groups.map(([id,ko,en])=>Object.freeze({id,ko,en})),Object.freeze({id:'legacy',ko:'기존 메뉴',en:'Existing menu'})]);
export const menuForOwner=owner=>owner===2?petMenu:catalog.drinks;
