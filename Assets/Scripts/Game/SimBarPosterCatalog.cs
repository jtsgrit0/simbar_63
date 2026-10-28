/// <summary>Visually transcribed public posters. Artist identities are factual; booking numbers are game parameters.</summary>
public static class SimBarPosterCatalog
{
    public const string CheckedOn = "2026-10-28";
    public sealed class Artist
    {
        public readonly string id, name, nameEnglish, sourceUrl;
        public readonly int genreIndex, gameFee, gameDraw;
        public Artist(string id, string name, string nameEnglish, int genreIndex, int gameFee, int gameDraw, string sourceUrl)
        { this.id=id; this.name=name; this.nameEnglish=nameEnglish; this.genreIndex=genreIndex; this.gameFee=gameFee; this.gameDraw=gameDraw; this.sourceUrl=sourceUrl; }
    }
    public sealed class Poster
    {
        public readonly string id, venueId, date, title, sourceUrl, imagePath, evidence, instagramUrl, handle;
        public readonly int[] artistIndices;
        public Poster(string id, string venueId, string date, string title, string sourceUrl, string imagePath,
            string evidence, string instagramUrl, string handle, params int[] artistIndices)
        {
            this.id=id; this.venueId=venueId; this.date=date; this.title=title; this.sourceUrl=sourceUrl;
            this.imagePath=imagePath; this.evidence=evidence; this.instagramUrl=instagramUrl; this.handle=handle;
            this.artistIndices=artistIndices;
        }
    }
    // Append only. Global booking indices are the original twelve followed by these artists.
    private const string FfSource="https://indistreet.com/gigs/2026-10-15-fresh-thursday-mjr408fkzkogiebvc1nofiqx";
    private const string PetMarch="https://indistreet.com/en/gigs/ht8oxgbs9mg49y6ybg7fitvd";
    private const string PetJune="https://www.indiego.co.kr/events/482-%ED%8E%AB%EC%82%AC%EC%9A%B4%EC%A6%88-%EC%9D%B8%EB%94%94%EB%9D%BC%EC%9D%B4%EB%B8%8C-pet-sounds-indie-live";
    private const string BoogieSource="https://muzikey.com/events/b28ec687-4ec9-4336-a989-8f27662c3753";
    private const string SubriotSource="https://muzikey.com/events/4cab399e-3ddf-47e6-93a0-5b9886c4ece7";
    // Equal neutral fees and draw are deliberate game settings, not a ranking of real artists.
    public static readonly Artist[] Artists =
    {
        new Artist("poster-elliword","엘리워드","엘리워드",-1,120,16,FfSource),
        new Artist("poster-sign-of-elma","사인오브엘마","사인오브엘마",-1,120,16,FfSource),
        new Artist("poster-quinzel","퀸젤","퀸젤",-1,120,16,FfSource),
        new Artist("poster-birthday-syndrome","버스데이신드롬","버스데이신드롬",-1,120,16,FfSource),
        new Artist("poster-signal-intrusion","전파납치","Signal Intrusion",-1,120,16,PetMarch),
        new Artist("poster-long-time-no-shit","롱타임노쉿","Long Time No Shit",-1,120,16,PetMarch),
        new Artist("poster-alone-wolf-elegy-club","론울프엘리지클럽","A LONE WOLF ELEGY CLUB",-1,120,16,PetJune),
        new Artist("poster-rux","럭스","RUX",-1,120,16,PetJune),
        new Artist("poster-yeastrio","yeastrio","yeastrio",1,120,16,BoogieSource),
        new Artist("poster-oklp-group","OKLP GROUP","OKLP GROUP",1,120,16,BoogieSource),
        new Artist("poster-lagrangian","Lagrangian","Lagrangian",1,120,16,SubriotSource),
        new Artist("poster-newsboy-men","Newsboy Men","Newsboy Men",12,120,16,SubriotSource)
    };
    public static readonly Poster[] Entries =
    {
        new Poster("ff-fresh-20261015","ff","2026-10-15","Fresh Thursday · 홍대 FF",FfSource,"ScoutPosters/ff-20261015.jpg",
            "공개 공연 포스터 사본 · 개별 인스타 게시물 미확인","https://www.instagram.com/clubff_hongdae/","@clubff_hongdae · 공연 설명 연결",12,13,14,15),
        new Poster("pet-indie-20240315","pet-sounds","2024-03-15","Pet Sounds Indie Live",PetMarch,"ScoutPosters/pet-20240315.jpg",
            "계정명이 인쇄된 공개 포스터 사본 · 개별 게시물 미확인","https://www.instagram.com/petsoundsmusicpub/","@petsoundsmusicpub",16,17),
        new Poster("pet-indie-20260613","pet-sounds","2026-06-13","Pet Sounds Indie Live",PetJune,"ScoutPosters/pet-20260613.png",
            "공개 사본 이미지 확인 · 원 Instagram 게시물 링크 확인","https://www.instagram.com/p/DZZ68wgmhiH/","@petsoundsmusicpub",18,19),
        new Poster("boogie-week-20251013","boogie-woogie","2025-10-13–19","Boogie Woogie · Jazz Live",BoogieSource,"ScoutPosters/boogie-weekly.png",
            "계정명이 인쇄된 공개 주간 포스터 · 일부 출연진 발굴","https://www.instagram.com/boogiewoogieseoul/","@boogiewoogieseoul",20,21),
        new Poster("subriot-jazz-20251212","subriot","2025-12-12–14","Subriot HBC · Jazz / Blues",SubriotSource,"ScoutPosters/subriot-20251212.png",
            "계정명이 인쇄된 공개 포스터 · 일부 출연진 발굴","https://www.instagram.com/subriot_hbc/","@subriot_hbc",22,23)
    };
}
